using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests;

[Collection(nameof(PostgreSqlIntegrationTestCollection))]
public sealed class PostgreSqlNotificationIdempotencyTests
{
    private readonly PostgreSqlWebApplicationFactory _factory;

    public PostgreSqlNotificationIdempotencyTests(PostgreSqlWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Repeating_notification_with_same_recipient_and_key_creates_one_effect()
    {
        var userId = Guid.NewGuid();
        var deduplicationKey = $"v6-idempotency:{Guid.NewGuid():N}";
        await SeedUserAsync(userId);

        await CreateNotificationAsync(
            userId,
            deduplicationKey,
            "First notification",
            "The first delivery request.");
        await CreateNotificationAsync(
            userId,
            deduplicationKey,
            "Repeated notification",
            "The repeated delivery request.");

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notifications = await dbContext.Notifications
            .Where(notification => notification.UserId == userId)
            .ToListAsync();
        var notificationIds = notifications.Select(notification => notification.Id).ToArray();
        var outboxMessages = await dbContext.NotificationEmailOutboxMessages
            .Where(message => message.UserId == userId)
            .ToListAsync();

        var notification = Assert.Single(notifications);
        Assert.Equal(deduplicationKey, notification.DeduplicationKey);
        Assert.Equal("First notification", notification.Title);
        Assert.Single(outboxMessages);
        Assert.Contains(
            outboxMessages,
            message => message.NotificationId == notification.Id);
        Assert.All(
            outboxMessages,
            message => Assert.Contains(message.NotificationId, notificationIds));
    }

    private async Task CreateNotificationAsync(
        Guid userId,
        string deduplicationKey,
        string title,
        string message)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var writer = new DatabaseNotificationWriter(dbContext);

        await writer.CreateAsync(
            userId,
            NotificationType.System,
            title,
            message,
            resourceType: "V6",
            resourceId: Guid.NewGuid(),
            projectId: Guid.NewGuid(),
            sendEmail: true,
            deduplicationKey: deduplicationKey);
    }

    private async Task SeedUserAsync(Guid userId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Users.Add(User.Create(
            EmailAddress.Create($"v6-idempotency-{userId:N}@example.com"),
            DisplayName.Create("V6 Idempotency User"),
            UserRole.User,
            isActive: true,
            isEmailConfirmed: true,
            id: userId));
        await dbContext.SaveChangesAsync();
    }
}
