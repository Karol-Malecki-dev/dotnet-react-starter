using Application.DTOs.Notification;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shared.Responses;

namespace Infrastructure.Services;

public sealed class DatabaseNotificationService : INotificationService
{
    private readonly ApplicationDbContext _dbContext;

    public DatabaseNotificationService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ApiResponse<NotificationPageDto>> GetUserNotificationsAsync(Guid userId, int pageNumber, int pageSize, bool unreadOnly)
    {
        var safePageNumber = Math.Max(pageNumber, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var query = _dbContext.Set<Notification>()
            .AsNoTracking()
            .Where(notification => notification.UserId == userId);

        if (unreadOnly)
        {
            query = query.Where(notification => notification.ReadAt == null);
        }

        var totalCount = await query.CountAsync();
        var unreadCount = await _dbContext.Set<Notification>()
            .CountAsync(notification => notification.UserId == userId && notification.ReadAt == null);
        var items = await query
            .OrderByDescending(notification => notification.CreatedAt)
            .Skip((safePageNumber - 1) * safePageSize)
            .Take(safePageSize)
            .Select(notification => MapToDto(notification))
            .ToListAsync();

        return ApiResponse<NotificationPageDto>.Success(new NotificationPageDto
        {
            Items = items,
            PageNumber = safePageNumber,
            PageSize = safePageSize,
            TotalCount = totalCount,
            UnreadCount = unreadCount
        });
    }

    public async Task<ApiResponse<int>> GetUnreadCountAsync(Guid userId)
    {
        var count = await _dbContext.Set<Notification>()
            .CountAsync(notification => notification.UserId == userId && notification.ReadAt == null);
        return ApiResponse<int>.Success(count);
    }

    public async Task<ApiResponse<NotificationDto>> MarkAsReadAsync(Guid userId, Guid notificationId)
    {
        var notification = await _dbContext.Set<Notification>()
            .FirstOrDefaultAsync(candidate => candidate.Id == notificationId && candidate.UserId == userId);
        if (notification is null)
        {
            return ApiResponse<NotificationDto>.Error(404, "Notification not found");
        }

        notification.ReadAt ??= DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        return ApiResponse<NotificationDto>.Success(MapToDto(notification), "Notification marked as read");
    }

    public async Task<ApiResponse<int>> MarkAllAsReadAsync(Guid userId)
    {
        var notifications = await _dbContext.Set<Notification>()
            .Where(notification => notification.UserId == userId && notification.ReadAt == null)
            .ToListAsync();
        var readAt = DateTime.UtcNow;
        foreach (var notification in notifications)
        {
            notification.ReadAt = readAt;
        }

        await _dbContext.SaveChangesAsync();
        return ApiResponse<int>.Success(notifications.Count, "Notifications marked as read");
    }

    public async Task CreateAsync(Guid userId, NotificationType type, string title, string message, string? resourceType = null, Guid? resourceId = null)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("A notification requires a user, title, and message.");
        }

        _dbContext.Set<Notification>().Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Title = title.Trim(),
            Message = message.Trim(),
            ResourceType = string.IsNullOrWhiteSpace(resourceType) ? null : resourceType.Trim(),
            ResourceId = resourceId,
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();
    }

    private static NotificationDto MapToDto(Notification notification) => new()
    {
        Id = notification.Id,
        Type = notification.Type,
        Title = notification.Title,
        Message = notification.Message,
        ResourceType = notification.ResourceType,
        ResourceId = notification.ResourceId,
        CreatedAt = notification.CreatedAt,
        ReadAt = notification.ReadAt
    };
}
