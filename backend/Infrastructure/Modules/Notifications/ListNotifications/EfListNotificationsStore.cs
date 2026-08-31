using Application.DTOs.Notification;
using Application.Modules.Notifications.ListNotifications;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Notifications.ListNotifications;

/// <summary>
/// EF Core projection for notifications belonging to one user.
/// </summary>
public sealed class EfListNotificationsStore : IListNotificationsStore
{
    private readonly ApplicationDbContext _dbContext;

    public EfListNotificationsStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<NotificationPageDto> QueryAsync(
        ListNotificationsQuery query,
        CancellationToken cancellationToken = default)
    {
        var pageNumber = Math.Max(query.PageNumber, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var notifications = _dbContext.Set<Notification>()
            .AsNoTracking()
            .Where(notification => notification.UserId == query.UserId);

        if (query.UnreadOnly)
        {
            notifications = notifications.Where(notification => notification.ReadAt == null);
        }

        var totalCount = await notifications.CountAsync(cancellationToken);
        var unreadCount = await _dbContext.Set<Notification>()
            .CountAsync(notification => notification.UserId == query.UserId && notification.ReadAt == null, cancellationToken);
        var items = await notifications
            .OrderByDescending(notification => notification.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(notification => new NotificationDto
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
            })
            .ToListAsync(cancellationToken);

        return new NotificationPageDto
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount,
            UnreadCount = unreadCount
        };
    }
}