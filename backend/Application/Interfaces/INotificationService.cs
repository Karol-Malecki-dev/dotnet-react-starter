using Application.DTOs.Notification;
using Domain.Enums;
using Shared.Responses;

namespace Application.Interfaces;

public interface INotificationService
{
    Task<ApiResponse<NotificationPageDto>> GetUserNotificationsAsync(Guid userId, int pageNumber, int pageSize, bool unreadOnly);
    Task<ApiResponse<int>> GetUnreadCountAsync(Guid userId);
    Task<ApiResponse<NotificationDto>> MarkAsReadAsync(Guid userId, Guid notificationId);
    Task<ApiResponse<int>> MarkAllAsReadAsync(Guid userId);
    Task<ApiResponse<NotificationEmailPreferenceDto>> GetEmailPreferenceAsync(Guid userId);
    Task<ApiResponse<NotificationEmailPreferenceDto>> UpdateEmailPreferenceAsync(Guid userId, bool isEmailEnabled);
    Task CreateAsync(Guid userId, NotificationType type, string title, string message, string? resourceType = null, Guid? resourceId = null, Guid? projectId = null);
}
