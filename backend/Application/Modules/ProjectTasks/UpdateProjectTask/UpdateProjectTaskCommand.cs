using Application.Features.ProjectManagement.Tasks;
using MediatR;
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
    string? ExpectedConcurrencyStamp = null)
    : IRequest<ProjectOperationResult<ProjectTaskView>>;

