namespace Domain.Entities;

/// <summary>
/// A durable discussion entry written by a project member on a project task.
/// </summary>
public class ProjectTaskComment
{
    /// <summary>Unique identifier for the comment.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Identifier of the task the comment belongs to.</summary>
    public Guid ProjectTaskId { get; set; }

    /// <summary>Identifier of the user who wrote the comment.</summary>
    public Guid AuthorUserId { get; set; }

    /// <summary>Trimmed, user-provided comment content.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>UTC timestamp at which the comment was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ProjectTask ProjectTask { get; set; } = null!;
    public User AuthorUser { get; set; } = null!;
}
