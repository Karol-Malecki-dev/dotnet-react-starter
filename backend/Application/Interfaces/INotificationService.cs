using Application.DTOs.Notification;
using Domain.Enums;
using Shared.Responses;

namespace Application.Interfaces;

public interface INotificationService
{
    Task<ApiResponse<NotificationPageDto>> GetUserNotificationsAsync(Guid userId, int pageNumber, int pageSize, bool unreadOnly, CancellationToken cancellationToken = default);
    Task<ApiResponse<int>> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ApiResponse<NotificationDto>> MarkAsReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default);
    Task<ApiResponse<int>> MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ApiResponse<NotificationEmailPreferenceDto>> GetEmailPreferenceAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ApiResponse<NotificationEmailPreferenceDto>> UpdateEmailPreferenceAsync(Guid userId, bool? isEmailEnabled, bool? isTaskDeadlineReminderEmailEnabled, CancellationToken cancellationToken = default);
    Task CreateAsync(Guid userId, NotificationType type, string title, string message, string? resourceType = null, Guid? resourceId = null, Guid? projectId = null, bool sendEmail = true, CancellationToken cancellationToken = default);
}
