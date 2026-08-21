using Application.Features.Projects;

namespace Application.Features.ProjectManagement.Tasks;

/// <summary>
/// Handles state-changing ProjectTask use cases.
/// </summary>
public interface IProjectTaskCommandService
{
    Task<ProjectOperationResult<ProjectTaskView>> CreateProjectTaskAsync(CreateProjectTaskCommand command);
    Task<ProjectOperationResult<ProjectTaskView>> UpdateProjectTaskAsync(UpdateProjectTaskCommand command);
    Task<ProjectOperationResult<ProjectTaskView>> UpdateProjectTaskStatusAsync(UpdateProjectTaskStatusCommand command);
    Task<ProjectOperationResult<bool>> DeleteProjectTaskAsync(Guid userId, Guid projectId, Guid taskId);
}