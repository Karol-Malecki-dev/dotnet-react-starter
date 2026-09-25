namespace Domain.Entities;

public sealed class NotificationEmailOutboxMessage
{
    public const int MaxAttempts = 3;

    public Guid Id { get; set; }
    public Guid NotificationId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? LastError { get; set; }
    /// <summary>
    /// Identifies the worker currently allowed to finalize this message.
    /// </summary>
    public Guid? ProcessingLeaseId { get; set; }

    /// <summary>
    /// UTC time after which another worker may reclaim the message.
    /// </summary>
    public DateTime? ProcessingLeaseExpiresAt { get; set; }

    /// <summary>
    /// UTC time when retry exhaustion moved this message to dead-letter state.
    /// </summary>
    public DateTime? DeadLetteredAt { get; set; }

    public Notification Notification { get; set; } = null!;
    public User User { get; set; } = null!;
}
