using Application.Features.Projects;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class DatabaseProjectService : IProjectApplicationService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly INotificationService _notificationService;

    public DatabaseProjectService(ApplicationDbContext dbContext, INotificationService notificationService)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
    }

    public async Task<ProjectOperationResult<List<ProjectView>>> GetUserProjectsAsync(Guid ownerId, bool includeArchived = false, string scope = "all")
    {
        var projects = await _dbContext.Projects
            .AsNoTracking()
            .Where(project => (scope == "owned" ? project.OwnerId == ownerId
                : scope == "member" ? project.OwnerId != ownerId && project.Members.Any(member => member.UserId == ownerId && member.User.IsActive)
                : project.OwnerId == ownerId || project.Members.Any(member => member.UserId == ownerId && member.User.IsActive))
                && (includeArchived || !project.IsArchived))
            .OrderByDescending(project => project.UpdatedAt)
            .Select(project => new ProjectView(
                project.Id,
                project.Name,
                project.Description,
                project.OwnerId,
                project.CreatedAt,
                project.UpdatedAt,
                project.IsArchived,
                project.OwnerId == ownerId
                    ? ProjectMemberRole.Owner
                    : project.Members.Where(member => member.UserId == ownerId).Select(member => member.Role).FirstOrDefault()))
            .ToListAsync();

        return ProjectOperationResult<List<ProjectView>>.Success(projects);
    }

    public async Task<ProjectOperationResult<ProjectView>> GetProjectAsync(Guid ownerId, Guid projectId, bool includeArchived = false)
    {
        var project = await _dbContext.Projects
            .AsNoTracking()
            .Include(candidate => candidate.Members)
            .FirstOrDefaultAsync(project => project.Id == projectId
                && (project.OwnerId == ownerId || project.Members.Any(member => member.UserId == ownerId && member.User.IsActive))
                && (includeArchived || !project.IsArchived));

        return project is null
                ? ProjectOperationResult<ProjectView>.Failure(ProjectOperationStatus.NotFound, "Project not found")
                : ProjectOperationResult<ProjectView>.Success(MapToView(project, ownerId));
    }

    public async Task<ProjectOperationResult<ProjectView>> CreateProjectAsync(CreateProjectCommand command)
    {
        var project = new Project
        {
            OwnerId = command.OwnerId,
            Name = command.Name.Trim(),
            Description = NormalizeDescription(command.Description)
        };

        _dbContext.Projects.Add(project);
        _dbContext.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = project.Id,
            UserId = command.OwnerId,
            Role = ProjectMemberRole.Owner
        });
        AddActivity(project.Id, command.OwnerId, "project.created", $"created the project '{project.Name}'.");
        await _dbContext.SaveChangesAsync();

        return ProjectOperationResult<ProjectView>.Success(MapToView(project, command.OwnerId), "Project created", 201);
    }

    public async Task<ProjectOperationResult<ProjectView>> UpdateProjectAsync(UpdateProjectCommand command)
    {
        var project = await _dbContext.Projects
            .FirstOrDefaultAsync(project => project.Id == command.ProjectId && project.OwnerId == command.OwnerId);

        if (project is null)
        {
            return ProjectOperationResult<ProjectView>.Failure(ProjectOperationStatus.NotFound, "Project not found");
        }

        if (project.IsArchived)
        {
            return ProjectOperationResult<ProjectView>.Failure(ProjectOperationStatus.Conflict, "Archived project cannot be updated");
        }

        project.Name = command.Name.Trim();
        project.Description = NormalizeDescription(command.Description);
        project.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return ProjectOperationResult<ProjectView>.Success(MapToView(project), "Project updated");
    }

    public async Task<ProjectOperationResult<bool>> ArchiveProjectAsync(Guid ownerId, Guid projectId)
    {
        var project = await _dbContext.Projects
            .FirstOrDefaultAsync(project => project.Id == projectId && project.OwnerId == ownerId);

        if (project is null)
        {
            return ProjectOperationResult<bool>.Failure(ProjectOperationStatus.NotFound, "Project not found");
        }

        if (project.IsArchived)
        {
            return ProjectOperationResult<bool>.Success(true, "Project already archived");
        }

        project.IsArchived = true;
        project.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return ProjectOperationResult<bool>.Success(true, "Project archived");
    }

    public async Task<ProjectOperationResult<List<ProjectMemberView>>> GetProjectMembersAsync(Guid ownerId, Guid projectId)
    {
        if (!await HasProjectAccessAsync(ownerId, projectId))
        {
            return ProjectOperationResult<List<ProjectMemberView>>.Failure(ProjectOperationStatus.NotFound, "Project not found");
        }

        var members = await _dbContext.ProjectMembers
            .AsNoTracking()
            .Where(member => member.ProjectId == projectId && member.User.IsActive)
            .OrderBy(member => member.User.DisplayName)
            .Select(member => new ProjectMemberView(
                member.UserId,
                member.User.DisplayName,
                member.User.Email,
                member.Role,
                member.AddedAt))
            .ToListAsync();

        return ProjectOperationResult<List<ProjectMemberView>>.Success(members);
    }

    public async Task<ProjectOperationResult<List<ProjectMemberUserView>>> GetAvailableProjectMembersAsync(Guid ownerId, Guid projectId)
    {
        if (!await OwnedProjectExistsAsync(ownerId, projectId))
        {
            return ProjectOperationResult<List<ProjectMemberUserView>>.Failure(ProjectOperationStatus.NotFound, "Project not found");
        }

        var memberIds = _dbContext.ProjectMembers
            .Where(member => member.ProjectId == projectId)
            .Select(member => member.UserId);

        var users = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.IsActive && !memberIds.Contains(user.Id))
            .OrderBy(user => user.DisplayName)
            .Select(user => new ProjectMemberUserView(user.Id, user.DisplayName, user.Email))
            .ToListAsync();

        return ProjectOperationResult<List<ProjectMemberUserView>>.Success(users);
    }

    public async Task<ProjectOperationResult<ProjectMemberView>> AddProjectMemberAsync(Guid ownerId, Guid projectId, Guid userId)
    {
        if (!await OwnedProjectExistsAsync(ownerId, projectId))
        {
            return ProjectOperationResult<ProjectMemberView>.Failure(ProjectOperationStatus.NotFound, "Project not found");
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(candidate => candidate.Id == userId && candidate.IsActive);
        if (user is null)
        {
            return ProjectOperationResult<ProjectMemberView>.Failure(ProjectOperationStatus.NotFound, "User not found or inactive");
        }

        if (await _dbContext.ProjectMembers.AnyAsync(member => member.ProjectId == projectId && member.UserId == userId))
        {
            return ProjectOperationResult<ProjectMemberView>.Failure(ProjectOperationStatus.Conflict, "User is already a project member");
        }

        var member = new ProjectMember { ProjectId = projectId, UserId = userId };
        _dbContext.ProjectMembers.Add(member);
        AddActivity(projectId, ownerId, "member.added", $"added {user.DisplayName} to the project.");
        await _dbContext.SaveChangesAsync();

        var projectName = await _dbContext.Projects
            .Where(project => project.Id == projectId)
            .Select(project => project.Name)
            .FirstAsync();
        await _notificationService.CreateAsync(
            user.Id,
            NotificationType.ProjectInvitation,
            "You joined a project",
            $"You were added to the project '{projectName}'.",
            "Project",
            projectId);

        return ProjectOperationResult<ProjectMemberView>.Success(new ProjectMemberView(
            user.Id,
            user.DisplayName,
            user.Email,
            member.Role,
            member.AddedAt), "Project member added", 201);
    }

    public async Task<ProjectOperationResult<ProjectMemberView>> UpdateProjectMemberRoleAsync(Guid ownerId, Guid projectId, Guid userId, ProjectMemberRole role)
    {
        if (!await OwnedProjectExistsAsync(ownerId, projectId))
        {
            return ProjectOperationResult<ProjectMemberView>.Failure(ProjectOperationStatus.NotFound, "Project not found");
        }

        if (userId == ownerId || role == ProjectMemberRole.Owner)
        {
            return ProjectOperationResult<ProjectMemberView>.Failure(ProjectOperationStatus.Conflict, "The project owner role cannot be changed");
        }

        if (role is not ProjectMemberRole.Member and not ProjectMemberRole.Viewer)
        {
            return ProjectOperationResult<ProjectMemberView>.Failure(ProjectOperationStatus.ValidationError, "Invalid project member role");
        }

        var member = await _dbContext.ProjectMembers
            .Include(candidate => candidate.User)
            .FirstOrDefaultAsync(candidate => candidate.ProjectId == projectId && candidate.UserId == userId);
        if (member is null)
        {
            return ProjectOperationResult<ProjectMemberView>.Failure(ProjectOperationStatus.NotFound, "Project member not found");
        }

        member.Role = role;
        await _dbContext.SaveChangesAsync();
        return ProjectOperationResult<ProjectMemberView>.Success(new ProjectMemberView(
            member.UserId,
            member.User.DisplayName,
            member.User.Email,
            member.Role,
            member.AddedAt), "Project member role updated");
    }

    public async Task<ProjectOperationResult<bool>> RemoveProjectMemberAsync(Guid ownerId, Guid projectId, Guid userId)
    {
        if (!await OwnedProjectExistsAsync(ownerId, projectId))
        {
            return ProjectOperationResult<bool>.Failure(ProjectOperationStatus.NotFound, "Project not found");
        }

        if (userId == ownerId)
        {
            return ProjectOperationResult<bool>.Failure(ProjectOperationStatus.Conflict, "Project owner cannot be removed");
        }

        var member = await _dbContext.ProjectMembers
            .FirstOrDefaultAsync(candidate => candidate.ProjectId == projectId && candidate.UserId == userId);
        if (member is null)
        {
            return ProjectOperationResult<bool>.Failure(ProjectOperationStatus.NotFound, "Project member not found");
        }

        var assignedTasks = await _dbContext.ProjectTasks
            .Where(task => task.ProjectId == projectId && task.AssignedUserId == userId)
            .ToListAsync();

        foreach (var task in assignedTasks)
        {
            task.AssignedUserId = null;
            task.UpdatedAt = DateTime.UtcNow;
        }

        _dbContext.ProjectMembers.Remove(member);
        AddActivity(projectId, ownerId, "member.removed", "removed a project member.");
        await _dbContext.SaveChangesAsync();

        return ProjectOperationResult<bool>.Success(true, "Project member removed");
    }

    public async Task<ProjectOperationResult<PagedProjectActivityView>> GetProjectActivitiesAsync(Guid userId, Guid projectId, int pageNumber, int pageSize)
    {
        if (!await HasProjectAccessAsync(userId, projectId))
        {
            return ProjectOperationResult<PagedProjectActivityView>.Failure(ProjectOperationStatus.NotFound, "Project not found");
        }

        var safePageNumber = Math.Max(pageNumber, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var query = _dbContext.ProjectActivities.AsNoTracking().Where(activity => activity.ProjectId == projectId);
        var totalCount = await query.CountAsync();
        var items = await query.OrderByDescending(activity => activity.CreatedAt)
            .Skip((safePageNumber - 1) * safePageSize).Take(safePageSize)
            .Select(activity => new ProjectActivityView(activity.Id, activity.Type, activity.Description, activity.ActorUserId, activity.ActorUser.DisplayName, activity.ProjectTaskId, activity.CreatedAt))
            .ToListAsync();
        return ProjectOperationResult<PagedProjectActivityView>.Success(new PagedProjectActivityView(items, safePageNumber, safePageSize, totalCount));
    }

    public async Task<ProjectOperationResult<ProjectDashboardView>> GetProjectDashboardAsync(Guid userId, Guid projectId)
    {
        if (!await HasProjectAccessAsync(userId, projectId))
        {
            return ProjectOperationResult<ProjectDashboardView>.Failure(ProjectOperationStatus.NotFound, "Project not found");
        }

        var today = DateTime.UtcNow.Date;
        var upcomingDeadline = today.AddDays(7);
        var tasks = await _dbContext.ProjectTasks.AsNoTracking()
            .Where(task => task.ProjectId == projectId)
            .ToListAsync();
        var recentActivities = await _dbContext.ProjectActivities.AsNoTracking()
            .Where(activity => activity.ProjectId == projectId)
            .OrderByDescending(activity => activity.CreatedAt)
            .Take(5)
            .Select(activity => new ProjectActivityView(activity.Id, activity.Type, activity.Description, activity.ActorUserId, activity.ActorUser.DisplayName, activity.ProjectTaskId, activity.CreatedAt))
            .ToListAsync();

        var overdueTasks = tasks.Where(task => task.DueDate.HasValue && task.DueDate.Value.Date < today && task.Status != ProjectTaskStatus.Done)
            .OrderBy(task => task.DueDate).Take(10).Select(MapDashboardTask).ToList();
        var upcomingTasks = tasks.Where(task => task.DueDate.HasValue && task.DueDate.Value.Date >= today && task.DueDate.Value.Date <= upcomingDeadline && task.Status != ProjectTaskStatus.Done)
            .OrderBy(task => task.DueDate).Take(10).Select(MapDashboardTask).ToList();

        return ProjectOperationResult<ProjectDashboardView>.Success(new ProjectDashboardView(
            tasks.Count,
            tasks.Count(task => task.Status == ProjectTaskStatus.Todo),
            tasks.Count(task => task.Status == ProjectTaskStatus.InProgress),
            tasks.Count(task => task.Status == ProjectTaskStatus.Done),
            tasks.Count(task => task.Priority == ProjectTaskPriority.Low),
            tasks.Count(task => task.Priority == ProjectTaskPriority.Normal),
            tasks.Count(task => task.Priority == ProjectTaskPriority.High),
            overdueTasks,
            upcomingTasks,
            recentActivities));
    }

    private Task<bool> OwnedProjectExistsAsync(Guid ownerId, Guid projectId)
        => _dbContext.Projects.AnyAsync(project => project.Id == projectId && project.OwnerId == ownerId);

    private Task<bool> HasProjectAccessAsync(Guid userId, Guid projectId)
        => _dbContext.Projects.AnyAsync(project => project.Id == projectId
            && (project.OwnerId == userId || project.Members.Any(member => member.UserId == userId && member.User.IsActive)));

    private void AddActivity(Guid projectId, Guid actorUserId, string type, string description)
    {
        _dbContext.ProjectActivities.Add(new ProjectActivity
        {
            ProjectId = projectId,
            ActorUserId = actorUserId,
            Type = type,
            Description = description
        });
    }

    private static ProjectTaskView MapDashboardTask(ProjectTask task) => new(
        task.Id, task.ProjectId, task.Title, task.Description, task.Status, task.Priority,
        task.DueDate, task.AssignedUserId, task.CreatedByUserId, task.CreatedAt, task.UpdatedAt);

    private static ProjectView MapToView(Project project, Guid? currentUserId = null) => new(
        project.Id,
        project.Name,
        project.Description,
        project.OwnerId,
        project.CreatedAt,
        project.UpdatedAt,
        project.IsArchived,
        project.OwnerId == currentUserId
            ? ProjectMemberRole.Owner
            : project.Members.FirstOrDefault(member => member.UserId == currentUserId)?.Role ?? ProjectMemberRole.Viewer);

    private static string? NormalizeDescription(string? description)
        => string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}