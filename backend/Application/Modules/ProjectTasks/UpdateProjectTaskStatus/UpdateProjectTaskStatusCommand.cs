using Application.Features.ProjectManagement.Tasks;
using Application.Features.Projects;
using Domain.Enums;

namespace Application.Modules.ProjectTasks.UpdateProjectTaskStatus;

/// <summary>
/// Represents the application input for changing a project task status.
/// </summary>
public sealed record UpdateProjectTaskStatusCommand(
    Guid UserId,
    Guid ProjectId,
    Guid TaskId,
    ProjectTaskStatus Status,
    string? ExpectedConcurrencyStamp = null);

/// <summary>
/// Executes the update-project-task-status use case without exposing persistence details to the API.
/// </summary>
public interface IUpdateProjectTaskStatusHandler
{
    Task<ProjectOperationResult<ProjectTaskView>> HandleAsync(
        UpdateProjectTaskStatusCommand command,
        CancellationToken cancellationToken = default);
}
