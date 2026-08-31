using Application.Features.ProjectManagement.Tasks;
using Application.Features.Projects;

namespace Application.Modules.ProjectTasks.DeleteProjectTask;

/// <summary>
/// Represents the application input for deleting a project task.
/// </summary>
public sealed record DeleteProjectTaskCommand(
    Guid UserId,
    Guid ProjectId,
    Guid TaskId,
    string? ExpectedConcurrencyStamp = null);

/// <summary>
/// Executes the delete-project-task use case without exposing persistence details to the API.
/// </summary>
public interface IDeleteProjectTaskHandler
{
    Task<ProjectOperationResult<bool>> HandleAsync(
        DeleteProjectTaskCommand command,
        CancellationToken cancellationToken = default);
}
