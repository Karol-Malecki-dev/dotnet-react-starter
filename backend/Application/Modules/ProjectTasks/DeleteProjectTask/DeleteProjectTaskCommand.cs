using Application.Modules.ProjectTasks.Shared;
using MediatR;
using Application.Modules.Projects.Shared;

namespace Application.Modules.ProjectTasks.DeleteProjectTask;

/// <summary>
/// Represents the application input for deleting a project task.
/// </summary>
public sealed record DeleteProjectTaskCommand(
    Guid UserId,
    Guid ProjectId,
    Guid TaskId,
    string? ExpectedConcurrencyStamp = null)
    : IRequest<ProjectOperationResult<bool>>;

