using Domain.Enums;

namespace Application.Features.Projects;

public sealed record ProjectTaskView(
    Guid Id,
    Guid ProjectId,
    string Title,
    string? Description,
    ProjectTaskStatus Status,
    ProjectTaskPriority Priority,
    DateTime? DueDate,
    Guid? AssignedUserId,
    Guid? CreatedByUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record PagedProjectTaskView(
    IReadOnlyList<ProjectTaskView> Items,
    int PageNumber,
    int PageSize,
    int TotalCount);

public sealed record ProjectTaskQuery(Guid UserId, Guid ProjectId, int PageNumber, int PageSize, string? Search);

public sealed record CreateProjectTaskCommand(
    Guid OwnerId,
    Guid ProjectId,
    string Title,
    string? Description,
    ProjectTaskPriority Priority,
    DateTime? DueDate,
    Guid? AssignedUserId);

public sealed record UpdateProjectTaskCommand(
    Guid OwnerId,
    Guid ProjectId,
    Guid TaskId,
    string Title,
    string? Description,
    ProjectTaskPriority Priority,
    DateTime? DueDate,
    Guid? AssignedUserId);

public sealed record UpdateProjectTaskStatusCommand(
    Guid OwnerId,
    Guid ProjectId,
    Guid TaskId,
    ProjectTaskStatus Status);
