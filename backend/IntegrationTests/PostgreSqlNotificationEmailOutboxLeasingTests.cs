using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace IntegrationTests;

[Collection(nameof(PostgreSqlNotificationEmailOutboxTestCollection))]
public sealed class PostgreSqlNotificationEmailOutboxLeasingTests
{
    private readonly PostgreSqlNotificationEmailOutboxWebApplicationFactory _factory;

    public PostgreSqlNotificationEmailOutboxLeasingTests(
        PostgreSqlNotificationEmailOutboxWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Concurrent_processors_send_a_claimed_message_only_once()
    {
        var outboxMessageId = await SeedOutboxMessageAsync();
        var sender = new BlockingNotificationEmailSender();

        await using var firstScope = _factory.Services.CreateAsyncScope();
        await using var secondScope = _factory.Services.CreateAsyncScope();
        var firstProcessor = CreateProcessor(firstScope, sender);
        var secondProcessor = CreateProcessor(secondScope, sender);

        var firstProcessing = firstProcessor.ProcessPendingMessagesAsync();
        var secondProcessing = secondProcessor.ProcessPendingMessagesAsync();

        await sender.WaitUntilSendStartsAsync();
        var timeout = Task.Delay(TimeSpan.FromSeconds(5));
        var completed = await Task.WhenAny(firstProcessing, secondProcessing, timeout);
        Assert.NotSame(timeout, completed);

        sender.Release();
        await Task.WhenAll(firstProcessing, secondProcessing);

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var dbContext = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var message = await dbContext.NotificationEmailOutboxMessages
            .SingleAsync(candidate => candidate.Id == outboxMessageId);

        Assert.Equal(1, sender.SendCount);
        Assert.NotNull(message.ProcessedAt);
        Assert.Null(message.ProcessingLeaseId);
        Assert.Null(message.ProcessingLeaseExpiresAt);
    }

    [Fact]
    public async Task Expired_lease_is_reclaimed_by_a_new_processor()
    {
        var outboxMessageId = await SeedOutboxMessageAsync();

        await using (var setupScope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var message = await dbContext.NotificationEmailOutboxMessages
                .SingleAsync(candidate => candidate.Id == outboxMessageId);
            message.ProcessingLeaseId = Guid.NewGuid();
            message.ProcessingLeaseExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await dbContext.SaveChangesAsync();
        }

        var sender = new RecordingNotificationEmailSender();
        await using var processorScope = _factory.Services.CreateAsyncScope();
        var processor = CreateProcessor(processorScope, sender);

        await processor.ProcessPendingMessagesAsync();

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persistedMessage = await verificationContext.NotificationEmailOutboxMessages
            .SingleAsync(candidate => candidate.Id == outboxMessageId);

        Assert.Equal(1, sender.SendCount);
        Assert.NotNull(persistedMessage.ProcessedAt);
        Assert.Null(persistedMessage.ProcessingLeaseId);
        Assert.Null(persistedMessage.ProcessingLeaseExpiresAt);
    }

    [Fact]
    public async Task Failed_delivery_clears_lease_and_schedules_retry()
    {
        var outboxMessageId = await SeedOutboxMessageAsync();
        var sender = new FailingNotificationEmailSender("SMTP unavailable");

        await using var processorScope = _factory.Services.CreateAsyncScope();
        var processor = CreateProcessor(processorScope, sender);
        await processor.ProcessPendingMessagesAsync();

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var dbContext = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var message = await dbContext.NotificationEmailOutboxMessages
            .SingleAsync(candidate => candidate.Id == outboxMessageId);

        Assert.Equal(1, sender.SendCount);
        Assert.Equal(1, message.AttemptCount);
        Assert.Equal("SMTP unavailable", message.LastError);
        Assert.True(message.NextAttemptAt > DateTime.UtcNow);
        Assert.Null(message.ProcessedAt);
        Assert.Null(message.ProcessingLeaseId);
        Assert.Null(message.ProcessingLeaseExpiresAt);
    }

    private NotificationEmailOutboxProcessor CreateProcessor(
        AsyncServiceScope scope,
        INotificationEmailSender sender)
    {
        return new NotificationEmailOutboxProcessor(
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(),
            sender,
            NullLogger<NotificationEmailOutboxProcessor>.Instance);
    }

    private async Task<Guid> SeedOutboxMessageAsync()
    {
        var userId = Guid.NewGuid();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Users.Add(User.Create(
            EmailAddress.Create($"v6-outbox-{userId:N}@example.com"),
            DisplayName.Create("V6 Outbox User"),
            UserRole.User,
            isActive: true,
            isEmailConfirmed: true,
            id: userId));
        await dbContext.SaveChangesAsync();

        var writer = new DatabaseNotificationWriter(dbContext);
        await writer.CreateAsync(
            userId,
            NotificationType.System,
            "Outbox test notification",
            "Outbox test message.",
            resourceType: "V6",
            resourceId: Guid.NewGuid(),
            sendEmail: true);

        return await dbContext.NotificationEmailOutboxMessages
            .Where(message => message.UserId == userId)
            .Select(message => message.Id)
            .SingleAsync();
    }

    private sealed class BlockingNotificationEmailSender : INotificationEmailSender
    {
        private readonly TaskCompletionSource _sendStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _sendCount;

        public int SendCount => Volatile.Read(ref _sendCount);

        public Task WaitUntilSendStartsAsync() =>
            _sendStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));

        public void Release() => _release.TrySetResult();

        public async Task SendAsync(
            string email,
            string displayName,
            string title,
            string message,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _sendCount);
            _sendStarted.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class RecordingNotificationEmailSender : INotificationEmailSender
    {
        private int _sendCount;

        public int SendCount => Volatile.Read(ref _sendCount);

        public Task SendAsync(
            string email,
            string displayName,
            string title,
            string message,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _sendCount);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingNotificationEmailSender : INotificationEmailSender
    {
        private readonly string _error;
        private int _sendCount;

        public FailingNotificationEmailSender(string error)
        {
            _error = error;
        }

        public int SendCount => Volatile.Read(ref _sendCount);

        public Task SendAsync(
            string email,
            string displayName,
            string title,
            string message,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _sendCount);
            throw new InvalidOperationException(_error);
        }
    }
}

public sealed class PostgreSqlNotificationEmailOutboxWebApplicationFactory : PostgreSqlWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            var workerDescriptor = services.FirstOrDefault(descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType == typeof(NotificationEmailOutboxWorker));
            if (workerDescriptor is not null)
            {
                services.Remove(workerDescriptor);
            }
        });
    }
}

[CollectionDefinition(nameof(PostgreSqlNotificationEmailOutboxTestCollection), DisableParallelization = true)]
public sealed class PostgreSqlNotificationEmailOutboxTestCollection
    : ICollectionFixture<PostgreSqlNotificationEmailOutboxWebApplicationFactory>;
