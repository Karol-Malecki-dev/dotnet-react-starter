using Application.DTOs.Project;
using Shared.Responses;

namespace Application.Interfaces;

public interface IProjectTaskService
{
    Task<ApiResponse<PagedResult<ProjectTaskDto>>> GetProjectTasksAsync(Guid userId, Guid projectId, ProjectTaskQueryDto query);
    Task<ApiResponse<ProjectTaskDto>> GetProjectTaskAsync(Guid ownerId, Guid projectId, Guid taskId);
    Task<ApiResponse<ProjectTaskDto>> CreateProjectTaskAsync(Guid ownerId, Guid projectId, CreateProjectTaskDto dto);
    Task<ApiResponse<ProjectTaskDto>> UpdateProjectTaskAsync(Guid ownerId, Guid projectId, Guid taskId, UpdateProjectTaskDto dto);
    Task<ApiResponse<ProjectTaskDto>> UpdateProjectTaskStatusAsync(Guid ownerId, Guid projectId, Guid taskId, UpdateProjectTaskStatusDto dto);
    Task<ApiResponse<bool>> DeleteProjectTaskAsync(Guid ownerId, Guid projectId, Guid taskId);
}