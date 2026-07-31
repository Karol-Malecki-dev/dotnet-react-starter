using Application.Features.Projects;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class DatabaseProjectTaskService : IProjectTaskApplicationService
{
    private readonly ApplicationDbContext _dbContext;

    public DatabaseProjectTaskService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProjectOperationResult<PagedProjectTaskView>> GetProjectTasksAsync(ProjectTaskQuery query)
    {
        if (!await HasProjectAccessAsync(query.UserId, query.ProjectId))
        {
            return ProjectOperationResult<PagedProjectTaskView>.Failure(ProjectOperationStatus.NotFound, "Project not found");
        }

        var pageNumber = Math.Max(query.PageNumber, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var taskQuery = _dbContext.ProjectTasks
            .AsNoTracking()
            .Where(task => task.ProjectId == query.ProjectId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            taskQuery = taskQuery.Where(task => task.Title.Contains(search) || (task.Description != null && task.Description.Contains(search)));
        }

        var totalCount = await taskQuery.CountAsync();
        var tasks = await taskQuery
            .OrderBy(task => task.Status)
            .ThenBy(task => task.DueDate)
            .ThenBy(task => task.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return ProjectOperationResult<PagedProjectTaskView>.Success(new PagedProjectTaskView(
            tasks.Select(MapToView).ToList(), pageNumber, pageSize, totalCount));
    }

    public async Task<ProjectOperationResult<ProjectTaskView>> GetProjectTaskAsync(Guid ownerId, Guid projectId, Guid taskId)
    {
        var task = await GetAccessibleActiveTaskAsync(ownerId, projectId, taskId);

        return task is null
            ? ProjectOperationResult<ProjectTaskView>.Failure(ProjectOperationStatus.NotFound, "Project task not found")
            : ProjectOperationResult<ProjectTaskView>.Success(MapToView(task));
    }

    public async Task<ProjectOperationResult<ProjectTaskView>> CreateProjectTaskAsync(CreateProjectTaskCommand command)
    {
        var role = await GetProjectRoleAsync(command.OwnerId, command.ProjectId);
        if (role is null)
        {
            return ProjectOperationResult<ProjectTaskView>.Failure(ProjectOperationStatus.NotFound, "Project not found");
        }

        var assignedUserError = await ValidateAssignedUserAsync(command.ProjectId, command.AssignedUserId);
        if (assignedUserError is not null)
        {
            return ProjectOperationResult<ProjectTaskView>.Failure(ProjectOperationStatus.ValidationError, assignedUserError);
        }

        if (role == ProjectMemberRole.Viewer)
        {
            return ProjectOperationResult<ProjectTaskView>.Failure(ProjectOperationStatus.Forbidden, "Viewer members cannot create tasks");
        }

        var task = new ProjectTask
        {
            ProjectId = command.ProjectId,
            Title = command.Title.Trim(),
            Description = NormalizeDescription(command.Description),
            Priority = command.Priority,
            DueDate = command.DueDate,
            AssignedUserId = command.AssignedUserId,
            CreatedByUserId = command.OwnerId
        };

        _dbContext.ProjectTasks.Add(task);
        await _dbContext.SaveChangesAsync();

        return ProjectOperationResult<ProjectTaskView>.Success(MapToView(task), "Project task created", 201);
    }

    public async Task<ProjectOperationResult<ProjectTaskView>> UpdateProjectTaskAsync(UpdateProjectTaskCommand command)
    {
        var task = await GetAccessibleActiveTaskAsync(command.OwnerId, command.ProjectId, command.TaskId);
        if (task is null)
        {
            return ProjectOperationResult<ProjectTaskView>.Failure(ProjectOperationStatus.NotFound, "Project task not found");
        }

        var role = await GetProjectRoleAsync(command.OwnerId, command.ProjectId);
        if (role == ProjectMemberRole.Viewer || (role == ProjectMemberRole.Member && task.CreatedByUserId != command.OwnerId))
        {
            return ProjectOperationResult<ProjectTaskView>.Failure(ProjectOperationStatus.Forbidden, "You cannot edit this task");
        }

        var assignedUserError = await ValidateAssignedUserAsync(command.ProjectId, command.AssignedUserId);
        if (assignedUserError is not null)
        {
            return ProjectOperationResult<ProjectTaskView>.Failure(ProjectOperationStatus.ValidationError, assignedUserError);
        }

        task.Title = command.Title.Trim();
        task.Description = NormalizeDescription(command.Description);
        task.Priority = command.Priority;
        task.DueDate = command.DueDate;
        task.AssignedUserId = command.AssignedUserId;
        task.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return ProjectOperationResult<ProjectTaskView>.Success(MapToView(task), "Project task updated");
    }

    public async Task<ProjectOperationResult<ProjectTaskView>> UpdateProjectTaskStatusAsync(UpdateProjectTaskStatusCommand command)
    {
        var task = await GetAccessibleActiveTaskAsync(command.OwnerId, command.ProjectId, command.TaskId);
        if (task is null)
        {
            return ProjectOperationResult<ProjectTaskView>.Failure(ProjectOperationStatus.NotFound, "Project task not found");
        }

        var role = await GetProjectRoleAsync(command.OwnerId, command.ProjectId);
        if (role == ProjectMemberRole.Viewer || (role == ProjectMemberRole.Member && task.CreatedByUserId != command.OwnerId))
        {
            return ProjectOperationResult<ProjectTaskView>.Failure(ProjectOperationStatus.Forbidden, "You cannot change this task status");
        }

        task.Status = command.Status;
        task.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return ProjectOperationResult<ProjectTaskView>.Success(MapToView(task), "Project task status updated");
    }

    public async Task<ProjectOperationResult<bool>> DeleteProjectTaskAsync(Guid ownerId, Guid projectId, Guid taskId)
    {
        var task = await GetAccessibleActiveTaskAsync(ownerId, projectId, taskId);
        if (task is null)
        {
            return ProjectOperationResult<bool>.Failure(ProjectOperationStatus.NotFound, "Project task not found");
        }

        var role = await GetProjectRoleAsync(ownerId, projectId);
        if (role == ProjectMemberRole.Viewer || (role == ProjectMemberRole.Member && task.CreatedByUserId != ownerId))
        {
            return ProjectOperationResult<bool>.Failure(ProjectOperationStatus.Forbidden, "You cannot delete this task");
        }

        _dbContext.ProjectTasks.Remove(task);
        await _dbContext.SaveChangesAsync();

        return ProjectOperationResult<bool>.Success(true, "Project task deleted");
    }

    private Task<bool> ActiveProjectExistsAsync(Guid ownerId, Guid projectId)
        => _dbContext.Projects.AnyAsync(project => project.Id == projectId
            && project.OwnerId == ownerId
            && !project.IsArchived);

    private async Task<ProjectMemberRole?> GetProjectRoleAsync(Guid userId, Guid projectId)
    {
        var project = await _dbContext.Projects.AsNoTracking().FirstOrDefaultAsync(project => project.Id == projectId && !project.IsArchived);
        if (project is null) return null;
        if (project.OwnerId == userId) return ProjectMemberRole.Owner;
        return await _dbContext.ProjectMembers.Where(member => member.ProjectId == projectId && member.UserId == userId && member.User.IsActive)
            .Select(member => (ProjectMemberRole?)member.Role).FirstOrDefaultAsync();
    }

    private async Task<bool> HasProjectAccessAsync(Guid userId, Guid projectId)
        => await GetProjectRoleAsync(userId, projectId) is not null;

    private Task<ProjectTask?> GetOwnedActiveTaskAsync(Guid ownerId, Guid projectId, Guid taskId)
        => _dbContext.ProjectTasks
            .Where(task => task.Id == taskId
                && task.ProjectId == projectId
                && task.Project.OwnerId == ownerId
                && !task.Project.IsArchived)
            .FirstOrDefaultAsync();

    private Task<ProjectTask?> GetAccessibleActiveTaskAsync(Guid userId, Guid projectId, Guid taskId)
        => _dbContext.ProjectTasks.Where(task => task.Id == taskId && task.ProjectId == projectId && !task.Project.IsArchived
            && (task.Project.OwnerId == userId || task.Project.Members.Any(member => member.UserId == userId && member.User.IsActive)))
            .FirstOrDefaultAsync();

    private async Task<string?> ValidateAssignedUserAsync(Guid projectId, Guid? assignedUserId)
    {
        if (!assignedUserId.HasValue)
        {
            return null;
        }

        return await _dbContext.ProjectMembers.AnyAsync(member => member.ProjectId == projectId
                && member.UserId == assignedUserId.Value
                && member.User.IsActive)
            ? null
            : "Assigned user is not an active member of this project";
    }

    private static ProjectTaskView MapToView(ProjectTask task) => new(
        task.Id,
        task.ProjectId,
        task.Title,
        task.Description,
        task.Status,
        task.Priority,
        task.DueDate,
        task.AssignedUserId,
        task.CreatedByUserId,
        task.CreatedAt,
        task.UpdatedAt);

    private static string? NormalizeDescription(string? description)
        => string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}