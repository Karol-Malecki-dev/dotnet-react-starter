namespace Domain.Entities;

public sealed class ProjectTaskAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectTaskId { get; set; }
    public Guid UploadedByUserId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ProjectTask ProjectTask { get; set; } = null!;
    public User UploadedByUser { get; set; } = null!;
}