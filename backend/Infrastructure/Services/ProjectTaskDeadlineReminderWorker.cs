using Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public sealed class ProjectTaskDeadlineReminderWorker : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProjectTaskDeadlineReminderWorker> _logger;

    public ProjectTaskDeadlineReminderWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ProjectTaskDeadlineReminderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
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
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Project task deadline reminder worker failed while processing tasks");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }
}