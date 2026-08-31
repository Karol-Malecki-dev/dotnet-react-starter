using Application.Modules.ProjectTasks.AssignmentNotifications;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.ProjectTasks.AssignmentNotifications;

/// <summary>
/// EF Core adapter that stages assignment notifications without committing the current unit of work.
/// </summary>
public sealed class EfProjectTaskAssignmentNotificationWriter : IProjectTaskAssignmentNotificationWriter
{
    private readonly ApplicationDbContext _dbContext;

    public EfProjectTaskAssignmentNotificationWriter(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddTaskAssignedNotificationAsync(
        Guid assigneeUserId,
        Guid projectId,
        Guid projectTaskId,
        string taskTitle,
        CancellationToken cancellationToken = default)
    {
        if (assigneeUserId == Guid.Empty
            || projectId == Guid.Empty
            || projectTaskId == Guid.Empty
            || string.IsNullOrWhiteSpace(taskTitle))
        {
            throw new ArgumentException("A task assignment notification requires valid identifiers and a title.");
        }

        var now = DateTime.UtcNow;
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = assigneeUserId,
            Type = NotificationType.TaskAssigned,
            Title = "You were assigned a task",
            Message = $"You were assigned the task '{taskTitle.Trim()}'.",
            ResourceType = "ProjectTask",
            ResourceId = projectTaskId,
            ProjectId = projectId,
            CreatedAt = now
        };

        _dbContext.Notifications.Add(notification);

        var emailEnabled = await _dbContext.NotificationEmailPreferences
            .Where(preference => preference.UserId == assigneeUserId)
            .Select(preference => (bool?)preference.IsEmailEnabled)
            .FirstOrDefaultAsync(cancellationToken) ?? true;

        if (emailEnabled)
        {
            _dbContext.NotificationEmailOutboxMessages.Add(new NotificationEmailOutboxMessage
            {
                Id = Guid.NewGuid(),
                NotificationId = notification.Id,
                UserId = assigneeUserId,
                CreatedAt = now,
                NextAttemptAt = now
            });
        }
    }
}
