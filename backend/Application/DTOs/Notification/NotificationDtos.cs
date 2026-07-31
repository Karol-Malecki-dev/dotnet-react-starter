using Domain.Enums;

namespace Application.DTOs.Notification;

public sealed class NotificationDto
{
    public Guid Id { get; init; }
    public NotificationType Type { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? ResourceType { get; init; }
    public Guid? ResourceId { get; init; }
    public Guid? ProjectId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ReadAt { get; init; }
    public bool IsRead => ReadAt.HasValue;
}

public sealed class NotificationPageDto
{
    public IReadOnlyList<NotificationDto> Items { get; init; } = [];
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int UnreadCount { get; init; }
}
