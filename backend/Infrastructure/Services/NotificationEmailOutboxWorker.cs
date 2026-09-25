using Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public sealed class NotificationEmailOutboxWorker : BackgroundService
{
    public const string WorkerName = "notification-email-outbox";
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationEmailOutboxWorker> _logger;
    private readonly BackgroundWorkerHealthState _healthState;

    public NotificationEmailOutboxWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationEmailOutboxWorker> logger,
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
                var processor = scope.ServiceProvider.GetRequiredService<INotificationEmailOutboxProcessor>();
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
                _logger.LogError(exception, "Notification email outbox worker failed while processing messages");
            }

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }
}
