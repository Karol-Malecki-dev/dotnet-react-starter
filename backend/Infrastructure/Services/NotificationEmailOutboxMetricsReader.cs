using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

/// <summary>
/// Reads notification email outbox metrics with one aggregate database query.
/// </summary>
public sealed class NotificationEmailOutboxMetricsReader : INotificationEmailOutboxMetricsReader
{
    private readonly ApplicationDbContext _dbContext;

    public NotificationEmailOutboxMetricsReader(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<NotificationEmailOutboxMetricsSnapshot> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var aggregate = await _dbContext.NotificationEmailOutboxMessages
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(messages => new
            {
                PendingMessageCount = messages.Count(message =>
                    message.ProcessedAt == null
                    && message.DeadLetteredAt == null
                    && message.AttemptCount < NotificationEmailOutboxMessage.MaxAttempts),
                OldestPendingMessageCreatedAt = messages
                    .Where(message =>
                        message.ProcessedAt == null
                        && message.DeadLetteredAt == null
                        && message.AttemptCount < NotificationEmailOutboxMessage.MaxAttempts)
                    .Select(message => (DateTime?)message.CreatedAt)
                    .Min(),
                DeadLetterMessageCount = messages.Count(message =>
                    message.ProcessedAt == null
                    && (message.DeadLetteredAt != null
                        || message.AttemptCount >= NotificationEmailOutboxMessage.MaxAttempts))
            })
            .SingleOrDefaultAsync(cancellationToken);

        var oldestPendingMessageAgeSeconds = aggregate?.OldestPendingMessageCreatedAt is { } createdAt
            ? Math.Max(0, (now - createdAt).TotalSeconds)
            : 0;

        return new NotificationEmailOutboxMetricsSnapshot(
            aggregate?.PendingMessageCount ?? 0,
            oldestPendingMessageAgeSeconds,
            aggregate?.DeadLetterMessageCount ?? 0);
    }
}
