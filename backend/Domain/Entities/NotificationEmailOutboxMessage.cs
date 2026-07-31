namespace Domain.Entities;

public sealed class NotificationEmailOutboxMessage
{
    public Guid Id { get; set; }
    public Guid NotificationId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? LastError { get; set; }

    public Notification Notification { get; set; } = null!;
    public User User { get; set; } = null!;
}
