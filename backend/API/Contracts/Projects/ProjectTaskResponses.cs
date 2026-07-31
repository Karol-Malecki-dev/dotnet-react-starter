using Domain.Enums;

namespace API.Contracts.Projects;

public sealed record ProjectTaskResponse(
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
    DateTime UpdatedAt,
    IReadOnlyList<string> Labels);

public sealed record PagedProjectTaskResponse(
    IReadOnlyList<ProjectTaskResponse> Items,
    int PageNumber,
    int PageSize,
    int TotalCount);
