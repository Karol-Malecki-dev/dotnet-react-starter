using Domain.Enums;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace UnitTests.Infrastructure;

public sealed class DatabaseNotificationWriterTests
{
    [Fact]
    public async Task Writer_ignores_duplicate_notification_for_same_recipient_and_key()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nameof(Writer_ignores_duplicate_notification_for_same_recipient_and_key))
            .Options;
        await using var context = new ApplicationDbContext(options);
        var writer = new DatabaseNotificationWriter(context);
        var userId = Guid.NewGuid();

        await writer.CreateAsync(userId, NotificationType.System, "Title", "Message", deduplicationKey: "task:1:assigned");
        await writer.CreateAsync(userId, NotificationType.System, "Title", "Message", deduplicationKey: "task:1:assigned");

        Assert.Single(context.Notifications);
    }

    [Fact]
    public async Task Writer_rejects_an_overlong_deduplication_key()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nameof(Writer_rejects_an_overlong_deduplication_key))
            .Options;
        await using var context = new ApplicationDbContext(options);
        var writer = new DatabaseNotificationWriter(context);

        await Assert.ThrowsAsync<ArgumentException>(() => writer.CreateAsync(
            Guid.NewGuid(),
            NotificationType.System,
            "Title",
            "Message",
            deduplicationKey: new string('x', 201)));
    }
}
