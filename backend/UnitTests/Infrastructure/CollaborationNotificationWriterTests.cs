using Domain.Enums;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace UnitTests.Infrastructure;

public sealed class CollaborationNotificationWriterTests
{
    [Fact]
    public async Task Stage_adds_notification_and_outbox_without_saving_the_unit_of_work()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nameof(Stage_adds_notification_and_outbox_without_saving_the_unit_of_work))
            .Options;
        await using var context = new ApplicationDbContext(options);
        var writer = new CollaborationNotificationWriter(context);
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        await writer.StageAsync(
            userId,
            NotificationType.ProjectMemberRoleChanged,
            "Role changed",
            "Your project role changed.",
            "project",
            projectId,
            projectId,
            $"project:{projectId}:role:Member");

        Assert.Single(context.ChangeTracker.Entries(), entry => entry.State == EntityState.Added && entry.Entity is global::Domain.Entities.Notification);
        Assert.Single(context.ChangeTracker.Entries(), entry => entry.State == EntityState.Added && entry.Entity is global::Domain.Entities.NotificationEmailOutboxMessage);
        Assert.Empty(await context.Notifications.AsNoTracking().ToListAsync());
    }
}