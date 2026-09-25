using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

/// <summary>
/// Claims and processes due notification email outbox messages.
/// </summary>
public sealed class NotificationEmailOutboxProcessor : INotificationEmailOutboxProcessor
{
    private const int BatchSize = 20;
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);

    private readonly ApplicationDbContext _dbContext;
    private readonly INotificationEmailSender _sender;
    private readonly ILogger<NotificationEmailOutboxProcessor> _logger;

    public NotificationEmailOutboxProcessor(
        ApplicationDbContext dbContext,
        INotificationEmailSender sender,
        ILogger<NotificationEmailOutboxProcessor> logger)
    {
        _dbContext = dbContext;
        _sender = sender;
        _logger = logger;
    }

    public async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var candidateIds = await _dbContext.NotificationEmailOutboxMessages
            .AsNoTracking()
            .Where(message => message.ProcessedAt == null
                && message.DeadLetteredAt == null
                && message.AttemptCount < NotificationEmailOutboxMessage.MaxAttempts
                && message.NextAttemptAt <= now
                && (message.ProcessingLeaseExpiresAt == null
                    || message.ProcessingLeaseExpiresAt <= now))
            .OrderBy(message => message.CreatedAt)
            .Take(BatchSize)
            .Select(message => message.Id)
            .ToListAsync(cancellationToken);

        if (candidateIds.Count == 0)
        {
            return;
        }

        var leaseId = Guid.NewGuid();
        var leaseExpiresAt = now.Add(LeaseDuration);
        var claimedCount = await _dbContext.NotificationEmailOutboxMessages
            .Where(message => candidateIds.Contains(message.Id)
                && message.ProcessedAt == null
                && message.DeadLetteredAt == null
                && message.AttemptCount < NotificationEmailOutboxMessage.MaxAttempts
                && message.NextAttemptAt <= now
                && (message.ProcessingLeaseExpiresAt == null
                    || message.ProcessingLeaseExpiresAt <= now))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(message => message.ProcessingLeaseId, leaseId)
                    .SetProperty(message => message.ProcessingLeaseExpiresAt, leaseExpiresAt),
                cancellationToken);

        if (claimedCount == 0)
        {
            return;
        }

        var messages = await _dbContext.NotificationEmailOutboxMessages
            .Include(message => message.Notification)
            .Include(message => message.User)
            .Where(message => message.ProcessingLeaseId == leaseId)
            .OrderBy(message => message.CreatedAt)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                await _sender.SendAsync(
                    message.User.Email.Value,
                    message.User.DisplayName.Value,
                    message.Notification.Title,
                    message.Notification.Message,
                    cancellationToken);

                var processedAt = DateTime.UtcNow;
                var updatedRows = await _dbContext.NotificationEmailOutboxMessages
                    .Where(candidate => candidate.Id == message.Id
                        && candidate.ProcessingLeaseId == leaseId
                        && candidate.ProcessedAt == null)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(candidate => candidate.ProcessedAt, processedAt)
                            .SetProperty(candidate => candidate.LastError, (string?)null)
                            .SetProperty(candidate => candidate.ProcessingLeaseId, (Guid?)null)
                            .SetProperty(candidate => candidate.ProcessingLeaseExpiresAt, (DateTime?)null)
                            .SetProperty(candidate => candidate.DeadLetteredAt, (DateTime?)null),
                        cancellationToken);

                if (updatedRows == 0)
                {
                    _logger.LogWarning(
                        "Notification email outbox lease was lost before message {OutboxMessageId} could be marked processed",
                        message.Id);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var attemptCount = message.AttemptCount + 1;
                var failureAt = DateTime.UtcNow;
                var nextAttemptAt = failureAt.AddMinutes(attemptCount);
                var deadLetteredAt = attemptCount >= NotificationEmailOutboxMessage.MaxAttempts
                    ? failureAt
                    : (DateTime?)null;
                var error = exception.Message[..Math.Min(exception.Message.Length, 2000)];
                var updatedRows = await _dbContext.NotificationEmailOutboxMessages
                    .Where(candidate => candidate.Id == message.Id
                        && candidate.ProcessingLeaseId == leaseId
                        && candidate.ProcessedAt == null)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(candidate => candidate.AttemptCount, attemptCount)
                            .SetProperty(candidate => candidate.LastError, error)
                            .SetProperty(candidate => candidate.NextAttemptAt, nextAttemptAt)
                            .SetProperty(candidate => candidate.ProcessingLeaseId, (Guid?)null)
                            .SetProperty(candidate => candidate.ProcessingLeaseExpiresAt, (DateTime?)null)
                            .SetProperty(candidate => candidate.DeadLetteredAt, deadLetteredAt),
                        cancellationToken);

                if (updatedRows == 0)
                {
                    _logger.LogWarning(
                        exception,
                        "Notification email outbox lease was lost before failure state could be recorded for message {OutboxMessageId}",
                        message.Id);
                }
                else
                {
                    _logger.LogWarning(
                        exception,
                        "Notification email delivery failed for outbox message {OutboxMessageId}",
                        message.Id);
                }
            }
        }
    }
}
