using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class CollaborationNotificationWriter : ICollaborationNotificationWriter
{
    private readonly ApplicationDbContext _dbContext;

    public CollaborationNotificationWriter(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task StageAsync(
        Guid userId,
        NotificationType type,
        string title,
        string message,
        string resourceType,
        Guid resourceId,
        Guid projectId,
        string deduplicationKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedKey = deduplicationKey.Trim();
        var duplicateIsTracked = _dbContext.ChangeTracker.Entries<Notification>()
            .Any(entry => entry.Entity.UserId == userId && entry.Entity.DeduplicationKey == normalizedKey);
        if (duplicateIsTracked || await _dbContext.Notifications.AsNoTracking().AnyAsync(
            notification => notification.UserId == userId && notification.DeduplicationKey == normalizedKey,
            cancellationToken))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            ResourceType = resourceType,
            ResourceId = resourceId,
            ProjectId = projectId,
            DeduplicationKey = normalizedKey,
            CreatedAt = now
        };
        _dbContext.Notifications.Add(notification);

        var emailEnabled = await _dbContext.NotificationEmailPreferences
            .Where(preference => preference.UserId == userId)
            .Select(preference => (bool?)preference.IsEmailEnabled)
            .FirstOrDefaultAsync(cancellationToken) ?? true;
        if (emailEnabled)
        {
            _dbContext.NotificationEmailOutboxMessages.Add(new NotificationEmailOutboxMessage
            {
                Id = Guid.NewGuid(),
                NotificationId = notification.Id,
                UserId = userId,
                CreatedAt = now,
                NextAttemptAt = now
            });
        }
    }
}