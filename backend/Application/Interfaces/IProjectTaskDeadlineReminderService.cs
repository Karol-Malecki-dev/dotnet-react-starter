namespace Application.Interfaces;

public interface IProjectTaskDeadlineReminderService
{
    Task ProcessDueTasksAsync(CancellationToken cancellationToken = default);
}