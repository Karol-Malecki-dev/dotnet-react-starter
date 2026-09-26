using Application.Features.ProjectManagement.Tasks;
using MediatR;
using Application.Features.Projects;

namespace Application.Modules.ProjectTasks.GetProjectTaskDetails;

/// <summary>
/// Represents the input required to read one project task.
/// </summary>
public sealed record GetProjectTaskDetailsQuery(
    Guid UserId,
    Guid ProjectId,
    Guid TaskId)
    : IRequest<ProjectOperationResult<ProjectTaskView>>;

