using Domain.Enums;

namespace Domain.Entities;

public sealed class ProjectTaskDeadlineReminder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectTaskId { get; set; }
    public Guid RecipientUserId { get; set; }
    public ProjectTaskDeadlineReminderType Type { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ProjectTask ProjectTask { get; set; } = null!;
    public User RecipientUser { get; set; } = null!;
}