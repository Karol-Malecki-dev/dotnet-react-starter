namespace Application.Interfaces;

/// <summary>
/// Reads aggregate operational metrics for the notification email outbox.
/// </summary>
public interface INotificationEmailOutboxMetricsReader
{
    /// <summary>
    /// Returns the current queue and dead-letter counters.
    /// </summary>
    Task<NotificationEmailOutboxMetricsSnapshot> ReadAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Snapshot exported by the notification email outbox metrics endpoint.
/// </summary>
/// <param name="PendingMessageCount">
/// Number of unprocessed, non-dead-letter messages, including messages waiting
/// for their next retry.
/// </param>
/// <param name="OldestPendingMessageAgeSeconds">
/// Age in seconds of the oldest unprocessed, non-dead-letter message, or zero
/// when the queue is empty.
/// </param>
/// <param name="DeadLetterMessageCount">
/// Number of unprocessed messages that exhausted their retry budget.
/// </param>
public sealed record NotificationEmailOutboxMetricsSnapshot(
    int PendingMessageCount,
    double OldestPendingMessageAgeSeconds,
    int DeadLetterMessageCount);
