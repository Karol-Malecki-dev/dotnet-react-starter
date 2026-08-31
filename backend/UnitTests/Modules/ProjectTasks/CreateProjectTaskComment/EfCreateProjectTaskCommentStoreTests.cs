using Application.Modules.ProjectTasks.CreateProjectTaskComment;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using Infrastructure.Data;
using Infrastructure.Modules.ProjectTasks.CreateProjectTaskComment;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace UnitTests.Modules.ProjectTasks.CreateProjectTaskComment;

public sealed class EfCreateProjectTaskCommentStoreTests
{
    [Fact]
    public async Task Create_stores_comment_and_notification_for_another_assignee_atomically()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nameof(Create_stores_comment_and_notification_for_another_assignee_atomically))
            .Options;
        await using var context = new ApplicationDbContext(options);
        var author = User.Create(
            EmailAddress.Create("author@example.com"),
            DisplayName.Create("Author"),
            UserRole.User,
            isEmailConfirmed: true);
        var assignee = User.Create(
            EmailAddress.Create("assignee@example.com"),
            DisplayName.Create("Assignee"),
            UserRole.User,
            isEmailConfirmed: true);
        var project = Project.Create(author.Id, "Project");
        var task = ProjectTask.Create(
            project.Id,
            "Task",
            null,
            ProjectTaskPriority.Normal,
            null,
            assignee.Id,
            author.Id);
        context.AddRange(author, assignee, project, task);
        await context.SaveChangesAsync();
        var writer = new CollaborationNotificationWriter(context);
        var store = new EfCreateProjectTaskCommentStore(context, writer);

        var comment = await store.CreateAsync(new CreateProjectTaskCommentCommand(
            author.Id,
            project.Id,
            task.Id,
            "Review completed"));

        var notification = await context.Notifications.SingleAsync();
        Assert.Equal(assignee.Id, notification.UserId);
        Assert.Equal(NotificationType.TaskCommented, notification.Type);
        Assert.Equal(task.Id, notification.ResourceId);
        Assert.Equal(project.Id, notification.ProjectId);
        Assert.Equal($"task:{task.Id}:comment:{comment.Id}", notification.DeduplicationKey);
        Assert.Single(context.NotificationEmailOutboxMessages);
    }
}
