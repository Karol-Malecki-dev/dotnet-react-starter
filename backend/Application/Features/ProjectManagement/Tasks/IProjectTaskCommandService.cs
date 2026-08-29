using Application.Features.Projects;

namespace Application.Features.ProjectManagement.Tasks;

/// <summary>
/// Handles state-changing ProjectTask use cases.
/// </summary>
public interface IProjectTaskCommandService
{
    Task<ProjectOperationResult<ProjectTaskView>> CreateProjectTaskAsync(CreateProjectTaskCommand command, CancellationToken cancellationToken = default);
    Task<ProjectOperationResult<ProjectTaskView>> UpdateProjectTaskAsync(UpdateProjectTaskCommand command, CancellationToken cancellationToken = default);
    Task<ProjectOperationResult<ProjectTaskView>> UpdateProjectTaskStatusAsync(UpdateProjectTaskStatusCommand command, CancellationToken cancellationToken = default);
    Task<ProjectOperationResult<bool>> DeleteProjectTaskAsync(Guid userId, Guid projectId, Guid taskId, CancellationToken cancellationToken = default, string? expectedConcurrencyStamp = null);
}