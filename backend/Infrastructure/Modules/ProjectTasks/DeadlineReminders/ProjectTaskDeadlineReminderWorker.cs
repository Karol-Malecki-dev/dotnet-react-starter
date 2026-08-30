using Application.Modules.ProjectTasks.DeadlineReminders;
using Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Modules.ProjectTasks.DeadlineReminders;

/// <summary>
/// Periodically processes project task deadline reminders in a scoped dependency lifetime.
/// </summary>
public sealed class ProjectTaskDeadlineReminderWorker : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromHours(1);
    public const string WorkerName = "project-task-deadline-reminders";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProjectTaskDeadlineReminderWorker> _logger;
    private readonly BackgroundWorkerHealthState _healthState;

    public ProjectTaskDeadlineReminderWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ProjectTaskDeadlineReminderWorker> logger,
        BackgroundWorkerHealthState healthState)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _healthState = healthState;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IProjectTaskDeadlineReminderProcessor>();
                await processor.ProcessDueTasksAsync(stoppingToken);
                _healthState.ReportSuccess(WorkerName);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _healthState.ReportFailure(WorkerName, exception);
                _logger.LogError(exception, "Project task deadline reminder worker failed while processing tasks");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }
}
