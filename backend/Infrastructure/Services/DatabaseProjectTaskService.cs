using Application.DTOs.Project;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shared.Responses;

namespace Infrastructure.Services;

public class DatabaseProjectTaskService : IProjectTaskService
{
    private readonly ApplicationDbContext _dbContext;

    public DatabaseProjectTaskService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ApiResponse<PagedResult<ProjectTaskDto>>> GetProjectTasksAsync(Guid userId, Guid projectId, ProjectTaskQueryDto query)
    {
        if (!await HasProjectAccessAsync(userId, projectId))
        {
            return ApiResponse<PagedResult<ProjectTaskDto>>.Error(404, "Project not found");
        }

        var pageNumber = Math.Max(query.PageNumber, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var taskQuery = _dbContext.ProjectTasks
            .AsNoTracking()
            .Where(task => task.ProjectId == projectId);
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

        return ApiResponse<PagedResult<ProjectTaskDto>>.Success(new PagedResult<ProjectTaskDto>
        {
            Items = tasks.Select(MapToDto).ToList(),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    public async Task<ApiResponse<ProjectTaskDto>> GetProjectTaskAsync(Guid ownerId, Guid projectId, Guid taskId)
    {
        var task = await GetAccessibleActiveTaskAsync(ownerId, projectId, taskId);

        return task is null
            ? ApiResponse<ProjectTaskDto>.Error(404, "Project task not found")
            : ApiResponse<ProjectTaskDto>.Success(MapToDto(task));
    }

    public async Task<ApiResponse<ProjectTaskDto>> CreateProjectTaskAsync(Guid ownerId, Guid projectId, CreateProjectTaskDto dto)
    {
        var role = await GetProjectRoleAsync(ownerId, projectId);
        if (role is null)
        {
            return ApiResponse<ProjectTaskDto>.Error(404, "Project not found");
        }

        var assignedUserError = await ValidateAssignedUserAsync(projectId, dto.AssignedUserId);
        if (assignedUserError is not null)
        {
            return ApiResponse<ProjectTaskDto>.Error(400, assignedUserError);
        }

        if (role == ProjectMemberRole.Viewer)
        {
            return ApiResponse<ProjectTaskDto>.Error(403, "Viewer members cannot create tasks");
        }

        var task = new ProjectTask
        {
            ProjectId = projectId,
            Title = dto.Title.Trim(),
            Description = NormalizeDescription(dto.Description),
            Priority = dto.Priority,
            DueDate = dto.DueDate,
            AssignedUserId = dto.AssignedUserId
            ,CreatedByUserId = ownerId
        };

        _dbContext.ProjectTasks.Add(task);
        await _dbContext.SaveChangesAsync();

        return ApiResponse<ProjectTaskDto>.Success(MapToDto(task), "Project task created", 201);
    }

    public async Task<ApiResponse<ProjectTaskDto>> UpdateProjectTaskAsync(Guid ownerId, Guid projectId, Guid taskId, UpdateProjectTaskDto dto)
    {
        var task = await GetAccessibleActiveTaskAsync(ownerId, projectId, taskId);
        if (task is null)
        {
            return ApiResponse<ProjectTaskDto>.Error(404, "Project task not found");
        }

        var role = await GetProjectRoleAsync(ownerId, projectId);
        if (role == ProjectMemberRole.Viewer || (role == ProjectMemberRole.Member && task.CreatedByUserId != ownerId))
        {
            return ApiResponse<ProjectTaskDto>.Error(403, "You cannot edit this task");
        }

        var assignedUserError = await ValidateAssignedUserAsync(projectId, dto.AssignedUserId);
        if (assignedUserError is not null)
        {
            return ApiResponse<ProjectTaskDto>.Error(400, assignedUserError);
        }

        task.Title = dto.Title.Trim();
        task.Description = NormalizeDescription(dto.Description);
        task.Priority = dto.Priority;
        task.DueDate = dto.DueDate;
        task.AssignedUserId = dto.AssignedUserId;
        task.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return ApiResponse<ProjectTaskDto>.Success(MapToDto(task), "Project task updated");
    }

    public async Task<ApiResponse<ProjectTaskDto>> UpdateProjectTaskStatusAsync(Guid ownerId, Guid projectId, Guid taskId, UpdateProjectTaskStatusDto dto)
    {
        var task = await GetAccessibleActiveTaskAsync(ownerId, projectId, taskId);
        if (task is null)
        {
            return ApiResponse<ProjectTaskDto>.Error(404, "Project task not found");
        }

        var role = await GetProjectRoleAsync(ownerId, projectId);
        if (role == ProjectMemberRole.Viewer || (role == ProjectMemberRole.Member && task.CreatedByUserId != ownerId))
        {
            return ApiResponse<ProjectTaskDto>.Error(403, "You cannot change this task status");
        }

        task.Status = dto.Status;
        task.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return ApiResponse<ProjectTaskDto>.Success(MapToDto(task), "Project task status updated");
    }

    public async Task<ApiResponse<bool>> DeleteProjectTaskAsync(Guid ownerId, Guid projectId, Guid taskId)
    {
        var task = await GetAccessibleActiveTaskAsync(ownerId, projectId, taskId);
        if (task is null)
        {
            return ApiResponse<bool>.Error(404, "Project task not found");
        }

        var role = await GetProjectRoleAsync(ownerId, projectId);
        if (role == ProjectMemberRole.Viewer || (role == ProjectMemberRole.Member && task.CreatedByUserId != ownerId))
        {
            return ApiResponse<bool>.Error(403, "You cannot delete this task");
        }

        _dbContext.ProjectTasks.Remove(task);
        await _dbContext.SaveChangesAsync();

        return ApiResponse<bool>.Success(true, "Project task deleted");
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

    private static ProjectTaskDto MapToDto(ProjectTask task) => new()
    {
        Id = task.Id,
        ProjectId = task.ProjectId,
        Title = task.Title,
        Description = task.Description,
        Status = task.Status,
        Priority = task.Priority,
        DueDate = task.DueDate,
        AssignedUserId = task.AssignedUserId,
        CreatedByUserId = task.CreatedByUserId,
        CreatedAt = task.CreatedAt,
        UpdatedAt = task.UpdatedAt
    };

    private static string? NormalizeDescription(string? description)
        => string.IsNullOrWhiteSpace(description) ? null : description.Trim();
}