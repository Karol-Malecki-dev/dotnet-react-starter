using Domain.Enums;

namespace API.Modules.ProjectTasks.UpdateProjectTask;

/// <summary>
/// HTTP request for updating a project task.
/// </summary>
public sealed record UpdateProjectTaskRequest(
    string Title,
    string? Description,
    ProjectTaskPriority Priority = ProjectTaskPriority.Normal,
    DateTime? DueDate = null,
    Guid? AssignedUserId = null,
    IReadOnlyList<string>? Labels = null,
    string? ConcurrencyStamp = null);
