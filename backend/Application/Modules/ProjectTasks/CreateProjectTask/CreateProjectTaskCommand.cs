using Application.Features.ProjectManagement.Tasks;
using Application.Features.Projects;
using Domain.Enums;

namespace Application.Modules.ProjectTasks.CreateProjectTask;

/// <summary>
/// Represents the application input for creating a task in a project.
/// </summary>
public sealed record CreateProjectTaskCommand(
    Guid OwnerId,
    Guid ProjectId,
    string Title,
    string? Description,
    ProjectTaskPriority Priority,
    DateTime? DueDate,
    Guid? AssignedUserId,
    IReadOnlyList<string> Labels);

/// <summary>
/// Executes the create-project-task use case without exposing persistence details to the API.
/// </summary>
public interface ICreateProjectTaskHandler
{
    Task<ProjectOperationResult<ProjectTaskView>> HandleAsync(
        CreateProjectTaskCommand command,
        CancellationToken cancellationToken = default);
}
