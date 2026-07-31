using Domain.Enums;

namespace API.Contracts.Projects;

public sealed record ProjectTaskQueryRequest(int PageNumber = 1, int PageSize = 20, string? Search = null);

public sealed record CreateProjectTaskRequest(
    string Title,
    string? Description,
    ProjectTaskPriority Priority = ProjectTaskPriority.Normal,
    DateTime? DueDate = null,
    Guid? AssignedUserId = null,
    IReadOnlyList<string>? Labels = null);

public sealed record UpdateProjectTaskRequest(
    string Title,
    string? Description,
    ProjectTaskPriority Priority = ProjectTaskPriority.Normal,
    DateTime? DueDate = null,
    Guid? AssignedUserId = null,
    IReadOnlyList<string>? Labels = null);

public sealed record UpdateProjectTaskStatusRequest(ProjectTaskStatus Status);
