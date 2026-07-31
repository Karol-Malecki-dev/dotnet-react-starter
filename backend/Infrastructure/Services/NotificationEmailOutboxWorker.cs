using Application.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public sealed class NotificationEmailOutboxWorker : BackgroundService
{
    private const int MaxAttempts = 3;
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
                await ProcessPendingMessagesAsync(stoppingToken);
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

    private async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<INotificationEmailSender>();
        var now = DateTime.UtcNow;
        var messages = await dbContext.NotificationEmailOutboxMessages
            .Include(message => message.Notification)
            .Include(message => message.User)
            .Where(message => message.ProcessedAt == null
                && message.AttemptCount < MaxAttempts
                && message.NextAttemptAt <= now)
            .OrderBy(message => message.CreatedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                await sender.SendAsync(
                    message.User.Email,
                    message.User.DisplayName,
                    message.Notification.Title,
                    message.Notification.Message);
                message.ProcessedAt = DateTime.UtcNow;
                message.LastError = null;
            }
            catch (Exception exception)
            {
                message.AttemptCount += 1;
                message.LastError = exception.Message[..Math.Min(exception.Message.Length, 2000)];
                message.NextAttemptAt = DateTime.UtcNow.AddMinutes(message.AttemptCount);
                _logger.LogWarning(exception, "Notification email delivery failed for outbox message {OutboxMessageId}", message.Id);
            }
        }

        if (messages.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
