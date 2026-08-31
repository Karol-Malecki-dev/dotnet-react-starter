using Application.Features.ProjectManagement.Tasks;
using Application.Features.Projects;

namespace Application.Modules.ProjectTasks.GetProjectTaskDetails;

/// <summary>
/// Represents the input required to read one project task.
/// </summary>
public sealed record GetProjectTaskDetailsQuery(
    Guid UserId,
    Guid ProjectId,
    Guid TaskId);

/// <summary>
/// Executes the get-project-task-details use case without exposing persistence details to the API.
/// </summary>
public interface IGetProjectTaskDetailsHandler
{
    Task<ProjectOperationResult<ProjectTaskView>> HandleAsync(
        GetProjectTaskDetailsQuery query,
        CancellationToken cancellationToken = default);
}
