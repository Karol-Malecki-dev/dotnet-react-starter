using Application.Modules.Projects.AddProjectMember;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.Projects.AddProjectMember;

/// <summary>
/// Stages the project-member notification and optional email delivery record in the current unit of work.
/// </summary>
public sealed class EfAddProjectMemberNotificationWriter : IAddProjectMemberNotificationWriter
{
    private readonly ApplicationDbContext _dbContext;

    public EfAddProjectMemberNotificationWriter(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddProjectMemberNotificationAsync(
        Guid userId,
        Guid projectId,
        string projectName,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = NotificationType.ProjectInvitation,
            Title = "You joined a project",
            Message = $"You were added to the project '{projectName}'.",
            ResourceType = "Project",
            ResourceId = projectId,
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
