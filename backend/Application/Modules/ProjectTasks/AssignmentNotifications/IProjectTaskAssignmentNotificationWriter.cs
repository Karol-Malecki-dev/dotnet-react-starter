namespace Application.Modules.ProjectTasks.AssignmentNotifications;

/// <summary>
/// Adds task-assignment notification records to the current persistence unit of work.
/// The caller owns the final save operation.
/// </summary>
public interface IProjectTaskAssignmentNotificationWriter
{
    /// <summary>
    /// Adds an in-app notification and, when enabled, an email outbox message for a new assignee.
    /// </summary>
    Task AddTaskAssignedNotificationAsync(
        Guid assigneeUserId,
        Guid projectId,
        Guid projectTaskId,
        string taskTitle,
        CancellationToken cancellationToken = default);
}
