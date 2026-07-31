namespace Application.Features.Projects;

public interface IProjectTaskApplicationService
{
    Task<ProjectOperationResult<PagedProjectTaskView>> GetProjectTasksAsync(ProjectTaskQuery query);
    Task<ProjectOperationResult<ProjectTaskView>> GetProjectTaskAsync(Guid ownerId, Guid projectId, Guid taskId);
    Task<ProjectOperationResult<ProjectTaskView>> CreateProjectTaskAsync(CreateProjectTaskCommand command);
    Task<ProjectOperationResult<ProjectTaskView>> UpdateProjectTaskAsync(UpdateProjectTaskCommand command);
    Task<ProjectOperationResult<ProjectTaskView>> UpdateProjectTaskStatusAsync(UpdateProjectTaskStatusCommand command);
    Task<ProjectOperationResult<bool>> DeleteProjectTaskAsync(Guid ownerId, Guid projectId, Guid taskId);
}
