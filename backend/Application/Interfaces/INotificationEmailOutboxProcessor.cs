namespace Application.Interfaces;

/// <summary>
/// Processes due notification email outbox messages.
/// </summary>
public interface INotificationEmailOutboxProcessor
{
    Task ProcessPendingMessagesAsync(CancellationToken cancellationToken = default);
}
