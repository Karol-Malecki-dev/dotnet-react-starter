using Application.Modules.ProjectTasks.AssignmentNotifications;
using Domain.Entities;
using Infrastructure.Data;
using Infrastructure.Modules.ProjectTasks.AssignmentNotifications;
using Microsoft.EntityFrameworkCore;
using UnitTests.TestHelpers;

namespace UnitTests.Modules.ProjectTasks.AssignmentNotifications;

public sealed class EfProjectTaskAssignmentNotificationWriterTests
{
    [Fact]
    public async Task AddTaskAssignedNotification_stages_notification_and_email_outbox_without_saving()
    {
        var options = UnitTestHelper.CreateInMemoryDatabaseOptions($"assignment-notification-{Guid.NewGuid():N}");
        await using var dbContext = new ApplicationDbContext(options);
        var writer = new EfProjectTaskAssignmentNotificationWriter(dbContext);
        var assigneeId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        await writer.AddTaskAssignedNotificationAsync(
            assigneeId,
            projectId,
            taskId,
            "Prepare release notes");

        var notification = Assert.Single(dbContext.Notifications.Local);
        Assert.Equal(assigneeId, notification.UserId);
        Assert.Equal(projectId, notification.ProjectId);
        Assert.Equal(taskId, notification.ResourceId);
        Assert.Equal("You were assigned a task", notification.Title);
        Assert.Equal(
            "You were assigned the task 'Prepare release notes'.",
            notification.Message);
        Assert.Equal(EntityState.Added, dbContext.Entry(notification).State);
        var outboxMessage = Assert.Single(dbContext.NotificationEmailOutboxMessages.Local);
        Assert.Equal(notification.Id, outboxMessage.NotificationId);
        Assert.Equal(assigneeId, outboxMessage.UserId);
        Assert.Equal(EntityState.Added, dbContext.Entry(outboxMessage).State);
    }

    [Fact]
    public async Task AddTaskAssignedNotification_does_not_stage_email_when_preference_is_disabled()
    {
        var options = UnitTestHelper.CreateInMemoryDatabaseOptions($"assignment-notification-disabled-{Guid.NewGuid():N}");
        await using var dbContext = new ApplicationDbContext(options);
        var assigneeId = Guid.NewGuid();
        dbContext.NotificationEmailPreferences.Add(new NotificationEmailPreference
        {
            UserId = assigneeId,
            IsEmailEnabled = false,
            IsTaskDeadlineReminderEmailEnabled = true,
            UpdatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
        var writer = new EfProjectTaskAssignmentNotificationWriter(dbContext);

        await writer.AddTaskAssignedNotificationAsync(
            assigneeId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Prepare release notes");

        Assert.Single(dbContext.Notifications.Local);
        Assert.Empty(dbContext.NotificationEmailOutboxMessages.Local);
    }
}
