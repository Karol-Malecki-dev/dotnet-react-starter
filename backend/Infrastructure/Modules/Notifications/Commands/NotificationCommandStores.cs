using Application.DTOs.Notification;
using Application.Modules.Notifications.Commands;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Notifications.Commands;

public sealed class EfMarkNotificationAsReadStore : IMarkNotificationAsReadStore
{
    private readonly ApplicationDbContext _dbContext;

    public EfMarkNotificationAsReadStore(ApplicationDbContext dbContext) => _dbContext = dbContext;

    public async Task<NotificationDto?> MarkAsReadAsync(
        Guid userId,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        var notification = await _dbContext.Notifications
            .FirstOrDefaultAsync(candidate => candidate.Id == notificationId && candidate.UserId == userId, cancellationToken);
        if (notification is null)
        {
            return null;
        }

        notification.ReadAt ??= DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(notification);
    }

    private static NotificationDto Map(Notification notification) => new()
    {
        Id = notification.Id,
        Type = notification.Type,
        Title = notification.Title,
        Message = notification.Message,
        ResourceType = notification.ResourceType,
        ResourceId = notification.ResourceId,
        ProjectId = notification.ProjectId,
        CreatedAt = notification.CreatedAt,
        ReadAt = notification.ReadAt
    };
}

public sealed class EfMarkAllNotificationsAsReadStore : IMarkAllNotificationsAsReadStore
{
    private readonly ApplicationDbContext _dbContext;

    public EfMarkAllNotificationsAsReadStore(ApplicationDbContext dbContext) => _dbContext = dbContext;

    public async Task<int> MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var notifications = await _dbContext.Notifications
            .Where(notification => notification.UserId == userId && notification.ReadAt == null)
            .ToListAsync(cancellationToken);
        var readAt = DateTime.UtcNow;
        foreach (var notification in notifications)
        {
            notification.ReadAt = readAt;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return notifications.Count;
    }
}

public sealed class EfUpdateNotificationEmailPreferenceStore : IUpdateNotificationEmailPreferenceStore
{
    private readonly ApplicationDbContext _dbContext;

    public EfUpdateNotificationEmailPreferenceStore(ApplicationDbContext dbContext) => _dbContext = dbContext;

    public async Task<NotificationEmailPreferenceDto> UpdateAsync(
        Guid userId,
        bool? isEmailEnabled,
        bool? isTaskDeadlineReminderEmailEnabled,
        CancellationToken cancellationToken = default)
    {
        var preference = await _dbContext.NotificationEmailPreferences
            .FirstOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken);
        if (preference is null)
        {
            preference = new NotificationEmailPreference
            {
                UserId = userId,
                IsEmailEnabled = isEmailEnabled ?? true,
                IsTaskDeadlineReminderEmailEnabled = isTaskDeadlineReminderEmailEnabled ?? true,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.NotificationEmailPreferences.Add(preference);
        }
        else
        {
            if (isEmailEnabled.HasValue) preference.IsEmailEnabled = isEmailEnabled.Value;
            if (isTaskDeadlineReminderEmailEnabled.HasValue) preference.IsTaskDeadlineReminderEmailEnabled = isTaskDeadlineReminderEmailEnabled.Value;
            preference.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return new NotificationEmailPreferenceDto
        {
            IsEmailEnabled = preference.IsEmailEnabled,
            IsTaskDeadlineReminderEmailEnabled = preference.IsTaskDeadlineReminderEmailEnabled
        };
    }
}