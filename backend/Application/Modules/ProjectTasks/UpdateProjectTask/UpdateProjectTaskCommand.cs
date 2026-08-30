using Application.Features.ProjectManagement.Tasks;
using Application.Features.Projects;
using Domain.Enums;

namespace Application.Modules.ProjectTasks.UpdateProjectTask;

/// <summary>
/// Represents the application input for updating a project task.
/// </summary>
public sealed record UpdateProjectTaskCommand(
    Guid UserId,
    Guid ProjectId,
    Guid TaskId,
    string Title,
    string? Description,
    ProjectTaskPriority Priority,
    DateTime? DueDate,
    Guid? AssignedUserId,
    IReadOnlyList<string> Labels,
    string? ExpectedConcurrencyStamp = null);

/// <summary>
/// Executes the update-project-task use case without exposing persistence details to the API.
/// </summary>
public interface IUpdateProjectTaskHandler
{
    Task<ProjectOperationResult<ProjectTaskView>> HandleAsync(
        UpdateProjectTaskCommand command,
        CancellationToken cancellationToken = default);
}
