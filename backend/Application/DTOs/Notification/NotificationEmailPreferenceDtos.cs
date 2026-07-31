namespace Application.DTOs.Notification;

public sealed class NotificationEmailPreferenceDto
{
    public bool IsEmailEnabled { get; init; }
}

public sealed class UpdateNotificationEmailPreferenceDto
{
    public bool IsEmailEnabled { get; init; }
}
