using Domain.Enums;

namespace API.Modules.ProjectTasks.CreateProjectTask;

/// <summary>
/// HTTP request for creating a project task.
/// </summary>
public sealed record CreateProjectTaskRequest(
    string Title,
    string? Description,
    ProjectTaskPriority Priority = ProjectTaskPriority.Normal,
    DateTime? DueDate = null,
    Guid? AssignedUserId = null,
    IReadOnlyList<string>? Labels = null);
