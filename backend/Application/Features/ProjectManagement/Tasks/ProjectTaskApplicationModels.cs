using Domain.Enums;

namespace Application.Features.ProjectManagement.Tasks;

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
    DateTime UpdatedAt,
    string ConcurrencyStamp,
    IReadOnlyList<string> Labels);
