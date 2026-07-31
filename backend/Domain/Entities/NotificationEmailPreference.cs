namespace Domain.Entities;

public sealed class NotificationEmailPreference
{
    public Guid UserId { get; set; }
    public bool IsEmailEnabled { get; set; } = true;
    public bool IsTaskDeadlineReminderEmailEnabled { get; set; } = true;
    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;
}
