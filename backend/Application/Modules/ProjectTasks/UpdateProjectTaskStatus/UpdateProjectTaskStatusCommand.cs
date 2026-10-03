using Application.Modules.ProjectTasks.Shared;
using MediatR;
using Application.Modules.Projects.Shared;
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
    string? ExpectedConcurrencyStamp = null)
    : IRequest<ProjectOperationResult<ProjectTaskView>>;

