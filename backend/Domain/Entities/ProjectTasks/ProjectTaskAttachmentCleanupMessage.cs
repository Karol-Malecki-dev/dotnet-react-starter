namespace Domain.Entities;

/// <summary>
/// Durable request to remove a task attachment from physical storage.
/// </summary>
public sealed class ProjectTaskAttachmentCleanupMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string StoredFileName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
    public int AttemptCount { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? LastError { get; set; }
}
