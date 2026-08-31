namespace Application.Modules.ProjectTasks.DeadlineReminders;

/// <summary>
/// Processes pending project task deadline reminders for assigned users.
/// </summary>
public interface IProjectTaskDeadlineReminderProcessor
{
    Task ProcessDueTasksAsync(CancellationToken cancellationToken = default);
}
