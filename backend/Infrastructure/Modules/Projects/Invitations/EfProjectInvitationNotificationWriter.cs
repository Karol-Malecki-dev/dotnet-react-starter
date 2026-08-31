using Application.Modules.Projects.Invitations;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Projects.Invitations;

/// <summary>
/// Stages invitation notifications and optional email outbox records in the current unit of work.
/// </summary>
public sealed class EfProjectInvitationNotificationWriter : IProjectInvitationNotificationWriter
{
    private readonly ApplicationDbContext _dbContext;

    public EfProjectInvitationNotificationWriter(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddInvitationCreatedNotificationAsync(
        Guid recipientUserId,
        Guid projectId,
        Guid invitationId,
        string projectName,
        string inviterDisplayName,
        CancellationToken cancellationToken = default)
        => AddAsync(
            recipientUserId,
            projectId,
            invitationId,
            "Project invitation",
            $"{inviterDisplayName} invited you to join '{projectName}'.",
            cancellationToken);

    public Task AddInvitationResponseNotificationAsync(
        Guid ownerUserId,
        Guid projectId,
        Guid invitationId,
        string projectName,
        string recipientDisplayName,
        ProjectInvitationStatus status,
        CancellationToken cancellationToken = default)
        => AddAsync(
            ownerUserId,
            projectId,
            invitationId,
            "Project invitation response",
            $"{recipientDisplayName} {status.ToString().ToLowerInvariant()} the invitation to '{projectName}'.",
            cancellationToken);

    private async Task AddAsync(
        Guid userId,
        Guid projectId,
        Guid invitationId,
        string title,
        string message,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || projectId == Guid.Empty || invitationId == Guid.Empty)
        {
            throw new ArgumentException("An invitation notification requires valid identifiers.");
        }

        var now = DateTime.UtcNow;
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = NotificationType.ProjectInvitation,
            Title = title,
            Message = message,
            ResourceType = "ProjectInvitation",
            ResourceId = invitationId,
            ProjectId = projectId,
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
