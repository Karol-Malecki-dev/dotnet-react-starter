using Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

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
                var service = scope.ServiceProvider.GetRequiredService<IProjectTaskDeadlineReminderService>();
                await service.ProcessDueTasksAsync(stoppingToken);
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