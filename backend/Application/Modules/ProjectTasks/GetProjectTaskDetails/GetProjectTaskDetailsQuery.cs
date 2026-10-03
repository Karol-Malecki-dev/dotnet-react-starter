using Application.Modules.ProjectTasks.Shared;
using MediatR;
using Application.Modules.Projects.Shared;

namespace Application.Modules.ProjectTasks.GetProjectTaskDetails;

/// <summary>
/// Represents the input required to read one project task.
/// </summary>
public sealed record GetProjectTaskDetailsQuery(
    Guid UserId,
    Guid ProjectId,
    Guid TaskId)
    : IRequest<ProjectOperationResult<ProjectTaskView>>;

