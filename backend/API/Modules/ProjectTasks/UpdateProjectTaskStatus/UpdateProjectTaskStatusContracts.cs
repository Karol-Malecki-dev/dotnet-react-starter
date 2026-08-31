using Domain.Enums;

namespace API.Modules.ProjectTasks.UpdateProjectTaskStatus;

/// <summary>
/// HTTP request for changing a project task status.
/// </summary>
public sealed record UpdateProjectTaskStatusRequest(
    ProjectTaskStatus Status,
    string? ConcurrencyStamp = null);
