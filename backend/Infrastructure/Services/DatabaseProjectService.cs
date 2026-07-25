using Application.DTOs.Project;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shared.Responses;

namespace Infrastructure.Services;

public class DatabaseProjectService : IProjectService
{
    private readonly ApplicationDbContext _dbContext;

    public DatabaseProjectService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ApiResponse<List<ProjectDto>>> GetUserProjectsAsync(Guid ownerId, bool includeArchived = false, string scope = "all")
    {
        var projects = await _dbContext.Projects
            .AsNoTracking()
            .Where(project => (scope == "owned" ? project.OwnerId == ownerId
                : scope == "member" ? project.OwnerId != ownerId && project.Members.Any(member => member.UserId == ownerId && member.User.IsActive)
                : project.OwnerId == ownerId || project.Members.Any(member => member.UserId == ownerId && member.User.IsActive))
                && (includeArchived || !project.IsArchived))
            .OrderByDescending(project => project.UpdatedAt)
            .Select(project => new ProjectDto
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                OwnerId = project.OwnerId,
                CreatedAt = project.CreatedAt,
                UpdatedAt = project.UpdatedAt,
                IsArchived = project.IsArchived,
                CurrentUserRole = project.OwnerId == ownerId
                    ? ProjectMemberRole.Owner
                    : project.Members.Where(member => member.UserId == ownerId).Select(member => member.Role).FirstOrDefault()
            })
            .ToListAsync();

        return ApiResponse<List<ProjectDto>>.Success(projects);
    }

    public async Task<ApiResponse<ProjectDto>> GetProjectAsync(Guid ownerId, Guid projectId, bool includeArchived = false)
    {
        var project = await _dbContext.Projects
            .AsNoTracking()
            .Include(candidate => candidate.Members)
            .FirstOrDefaultAsync(project => project.Id == projectId
                && (project.OwnerId == ownerId || project.Members.Any(member => member.UserId == ownerId && member.User.IsActive))
                && (includeArchived || !project.IsArchived));

        return project is null
            ? ApiResponse<ProjectDto>.Error(404, "Project not found")
            : ApiResponse<ProjectDto>.Success(MapToDto(project, ownerId));
    }

    public async Task<ApiResponse<ProjectDto>> CreateProjectAsync(Guid ownerId, CreateProjectDto dto)
    {
        var project = new Project
        {
            OwnerId = ownerId,
            Name = dto.Name.Trim(),
            Description = NormalizeDescription(dto.Description)
        };

        _dbContext.Projects.Add(project);
        _dbContext.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = project.Id,
            UserId = ownerId,
            Role = ProjectMemberRole.Owner
        });
        await _dbContext.SaveChangesAsync();

        return ApiResponse<ProjectDto>.Success(MapToDto(project, ownerId), "Project created", 201);
    }

    public async Task<ApiResponse<ProjectDto>> UpdateProjectAsync(Guid ownerId, Guid projectId, UpdateProjectDto dto)
    {
        var project = await _dbContext.Projects
            .FirstOrDefaultAsync(project => project.Id == projectId && project.OwnerId == ownerId);

        if (project is null)
        {
            return ApiResponse<ProjectDto>.Error(404, "Project not found");
        }

        if (project.IsArchived)
        {
            return ApiResponse<ProjectDto>.Error(409, "Archived project cannot be updated");
        }

        project.Name = dto.Name.Trim();
        project.Description = NormalizeDescription(dto.Description);
        project.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return ApiResponse<ProjectDto>.Success(MapToDto(project), "Project updated");
    }

    public async Task<ApiResponse<bool>> ArchiveProjectAsync(Guid ownerId, Guid projectId)
    {
        var project = await _dbContext.Projects
            .FirstOrDefaultAsync(project => project.Id == projectId && project.OwnerId == ownerId);

        if (project is null)
        {
            return ApiResponse<bool>.Error(404, "Project not found");
        }

        if (project.IsArchived)
        {
            return ApiResponse<bool>.Success(true, "Project already archived");
        }

        project.IsArchived = true;
        project.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return ApiResponse<bool>.Success(true, "Project archived");
    }

    public async Task<ApiResponse<List<ProjectMemberDto>>> GetProjectMembersAsync(Guid ownerId, Guid projectId)
    {
        if (!await HasProjectAccessAsync(ownerId, projectId))
        {
            return ApiResponse<List<ProjectMemberDto>>.Error(404, "Project not found");
        }

        var members = await _dbContext.ProjectMembers
            .AsNoTracking()
            .Where(member => member.ProjectId == projectId && member.User.IsActive)
            .OrderBy(member => member.User.DisplayName)
            .Select(member => new ProjectMemberDto
            {
                UserId = member.UserId,
                DisplayName = member.User.DisplayName,
                Email = member.User.Email,
                Role = member.Role,
                AddedAt = member.AddedAt
            })
            .ToListAsync();

        return ApiResponse<List<ProjectMemberDto>>.Success(members);
    }

    public async Task<ApiResponse<List<ProjectMemberUserDto>>> GetAvailableProjectMembersAsync(Guid ownerId, Guid projectId)
    {
        if (!await OwnedProjectExistsAsync(ownerId, projectId))
        {
            return ApiResponse<List<ProjectMemberUserDto>>.Error(404, "Project not found");
        }

        var memberIds = _dbContext.ProjectMembers
            .Where(member => member.ProjectId == projectId)
            .Select(member => member.UserId);

        var users = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.IsActive && !memberIds.Contains(user.Id))
            .OrderBy(user => user.DisplayName)
            .Select(user => new ProjectMemberUserDto
            {
                Id = user.Id,
                DisplayName = user.DisplayName,
                Email = user.Email
            })
            .ToListAsync();

        return ApiResponse<List<ProjectMemberUserDto>>.Success(users);
    }

    public async Task<ApiResponse<ProjectMemberDto>> AddProjectMemberAsync(Guid ownerId, Guid projectId, Guid userId)
    {
        if (!await OwnedProjectExistsAsync(ownerId, projectId))
        {
            return ApiResponse<ProjectMemberDto>.Error(404, "Project not found");
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(candidate => candidate.Id == userId && candidate.IsActive);
        if (user is null)
        {
            return ApiResponse<ProjectMemberDto>.Error(404, "User not found or inactive");
        }

        if (await _dbContext.ProjectMembers.AnyAsync(member => member.ProjectId == projectId && member.UserId == userId))
        {
            return ApiResponse<ProjectMemberDto>.Error(409, "User is already a project member");
        }

        var member = new ProjectMember { ProjectId = projectId, UserId = userId };
        _dbContext.ProjectMembers.Add(member);
        await _dbContext.SaveChangesAsync();

        return ApiResponse<ProjectMemberDto>.Success(new ProjectMemberDto
        {
            UserId = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email,
            Role = member.Role,
            AddedAt = member.AddedAt
        }, "Project member added", 201);
    }

    public async Task<ApiResponse<ProjectMemberDto>> UpdateProjectMemberRoleAsync(Guid ownerId, Guid projectId, Guid userId, ProjectMemberRole role)
    {
        if (!await OwnedProjectExistsAsync(ownerId, projectId))
        {
            return ApiResponse<ProjectMemberDto>.Error(404, "Project not found");
        }

        if (userId == ownerId || role == ProjectMemberRole.Owner)
        {
            return ApiResponse<ProjectMemberDto>.Error(409, "The project owner role cannot be changed");
        }

        if (role is not ProjectMemberRole.Member and not ProjectMemberRole.Viewer)
        {
            return ApiResponse<ProjectMemberDto>.Error(400, "Invalid project member role");
        }

        var member = await _dbContext.ProjectMembers
            .Include(candidate => candidate.User)
            .FirstOrDefaultAsync(candidate => candidate.ProjectId == projectId && candidate.UserId == userId);
        if (member is null)
        {
            return ApiResponse<ProjectMemberDto>.Error(404, "Project member not found");
        }

        member.Role = role;
        await _dbContext.SaveChangesAsync();
        return ApiResponse<ProjectMemberDto>.Success(new ProjectMemberDto
        {
            UserId = member.UserId,
            DisplayName = member.User.DisplayName,
            Email = member.User.Email,
            Role = member.Role,
            AddedAt = member.AddedAt
        }, "Project member role updated");
    }

    public async Task<ApiResponse<bool>> RemoveProjectMemberAsync(Guid ownerId, Guid projectId, Guid userId)
    {
        if (!await OwnedProjectExistsAsync(ownerId, projectId))
        {
            return ApiResponse<bool>.Error(404, "Project not found");
        }

        if (userId == ownerId)
        {
            return ApiResponse<bool>.Error(409, "Project owner cannot be removed");
        }

        var member = await _dbContext.ProjectMembers
            .FirstOrDefaultAsync(candidate => candidate.ProjectId == projectId && candidate.UserId == userId);
        if (member is null)
        {
            return ApiResponse<bool>.Error(404, "Project member not found");
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
        await _dbContext.SaveChangesAsync();

        return ApiResponse<bool>.Success(true, "Project member removed");
    }

    private Task<bool> OwnedProjectExistsAsync(Guid ownerId, Guid projectId)
        => _dbContext.Projects.AnyAsync(project => project.Id == projectId && project.OwnerId == ownerId);

    private Task<bool> HasProjectAccessAsync(Guid userId, Guid projectId)
        => _dbContext.Projects.AnyAsync(project => project.Id == projectId
            && (project.OwnerId == userId || project.Members.Any(member => member.UserId == userId && member.User.IsActive)));

    private static ProjectDto MapToDto(Project project, Guid? currentUserId = null) => new()
    {
        Id = project.Id,
        Name = project.Name,
        Description = project.Description,
        OwnerId = project.OwnerId,
        CreatedAt = project.CreatedAt,
        UpdatedAt = project.UpdatedAt,
        IsArchived = project.IsArchived,
        CurrentUserRole = project.OwnerId == currentUserId
            ? ProjectMemberRole.Owner
            : project.Members.FirstOrDefault(member => member.UserId == currentUserId)?.Role ?? ProjectMemberRole.Viewer
    };

    private static string? NormalizeDescription(string? description)
        => string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}