using Application.DTOs.Notification;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shared.Responses;

namespace Infrastructure.Services;

public sealed class DatabaseNotificationWriter : INotificationWriter
{
    private readonly ApplicationDbContext _dbContext;

    public DatabaseNotificationWriter(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task CreateAsync(Guid userId, NotificationType type, string title, string message, string? resourceType = null, Guid? resourceId = null, Guid? projectId = null, bool sendEmail = true, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("A notification requires a user, title, and message.");
        }

        var now = DateTime.UtcNow;
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Title = title.Trim(),
            Message = message.Trim(),
            ResourceType = string.IsNullOrWhiteSpace(resourceType) ? null : resourceType.Trim(),
            ResourceId = resourceId,
            ProjectId = projectId,
            CreatedAt = now
        };
        _dbContext.Notifications.Add(notification);

        var emailEnabled = await _dbContext.NotificationEmailPreferences
            .Where(preference => preference.UserId == userId)
            .Select(preference => (bool?)preference.IsEmailEnabled)
            .FirstOrDefaultAsync(cancellationToken) ?? true;
        if (sendEmail && emailEnabled)
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

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

}
