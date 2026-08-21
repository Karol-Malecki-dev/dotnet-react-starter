using Application.Features.Projects;

namespace Application.Features.ProjectManagement.Tasks;

/// <summary>
/// Handles read-only ProjectTask use cases.
/// </summary>
public interface IProjectTaskQueryService
{
    Task<ProjectOperationResult<PagedProjectTaskView>> GetProjectTasksAsync(ProjectTaskQuery query);
    Task<ProjectOperationResult<ProjectTaskView>> GetProjectTaskAsync(Guid userId, Guid projectId, Guid taskId);
}