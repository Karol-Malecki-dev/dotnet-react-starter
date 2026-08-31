using Application.Modules.ProjectTasks.Attachments;
using Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Modules.ProjectTasks.Attachments;

/// <summary>
/// Periodically processes durable task attachment cleanup messages.
/// </summary>
public sealed class ProjectTaskAttachmentCleanupWorker : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(15);
    public const string WorkerName = "project-task-attachment-cleanup";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProjectTaskAttachmentCleanupWorker> _logger;
    private readonly BackgroundWorkerHealthState _healthState;

    public ProjectTaskAttachmentCleanupWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ProjectTaskAttachmentCleanupWorker> logger,
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
                var processor = scope.ServiceProvider.GetRequiredService<IProjectTaskAttachmentCleanupProcessor>();
                await processor.ProcessPendingMessagesAsync(stoppingToken);
                _healthState.ReportSuccess(WorkerName);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _healthState.ReportFailure(WorkerName, exception);
                _logger.LogError(exception, "Project task attachment cleanup worker failed while processing messages");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }
}
