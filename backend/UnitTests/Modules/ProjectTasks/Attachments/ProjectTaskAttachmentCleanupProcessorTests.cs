using Application.Modules.ProjectTasks.Attachments;
using Domain.Entities;
using Infrastructure.Data;
using Infrastructure.Modules.ProjectTasks.Attachments;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UnitTests.TestHelpers;

namespace UnitTests.Modules.ProjectTasks.Attachments;

public sealed class ProjectTaskAttachmentCleanupProcessorTests
{
    [Fact]
    public async Task ProcessPendingMessages_marks_message_processed_after_storage_delete()
    {
        var options = UnitTestHelper.CreateInMemoryDatabaseOptions($"cleanup-success-{Guid.NewGuid():N}");
        await using var dbContext = new ApplicationDbContext(options);
        var message = CreateMessage();
        dbContext.ProjectTaskAttachmentCleanupMessages.Add(message);
        await dbContext.SaveChangesAsync();

        var storage = new Mock<IProjectTaskAttachmentStorage>();
        var processor = CreateProcessor(dbContext, storage);

        await processor.ProcessPendingMessagesAsync();

        storage.Verify(
            service => service.DeleteAsync(message.StoredFileName, It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.NotNull(message.ProcessedAt);
        Assert.Null(message.LastError);
        Assert.Equal(0, message.AttemptCount);
    }

    [Fact]
    public async Task ProcessPendingMessages_treats_missing_file_as_success()
    {
        var options = UnitTestHelper.CreateInMemoryDatabaseOptions($"cleanup-missing-{Guid.NewGuid():N}");
        await using var dbContext = new ApplicationDbContext(options);
        var message = CreateMessage();
        dbContext.ProjectTaskAttachmentCleanupMessages.Add(message);
        await dbContext.SaveChangesAsync();

        var directory = Directory.CreateTempSubdirectory("task-attachment-cleanup-");
        try
        {
            var environment = new Mock<IHostEnvironment>();
            environment.SetupGet(host => host.ContentRootPath).Returns(directory.FullName);
            var processor = new ProjectTaskAttachmentCleanupProcessor(
                dbContext,
                new LocalProjectTaskAttachmentStorage(environment.Object),
                NullLogger<ProjectTaskAttachmentCleanupProcessor>.Instance);

            await processor.ProcessPendingMessagesAsync();

            Assert.NotNull(message.ProcessedAt);
            Assert.Null(message.LastError);
        }
        finally
        {
            Directory.Delete(directory.FullName, recursive: true);
        }
    }

    [Fact]
    public async Task ProcessPendingMessages_records_retry_state_when_storage_delete_fails()
    {
        var options = UnitTestHelper.CreateInMemoryDatabaseOptions($"cleanup-retry-{Guid.NewGuid():N}");
        await using var dbContext = new ApplicationDbContext(options);
        var message = CreateMessage();
        dbContext.ProjectTaskAttachmentCleanupMessages.Add(message);
        await dbContext.SaveChangesAsync();

        var storage = new Mock<IProjectTaskAttachmentStorage>();
        storage
            .Setup(service => service.DeleteAsync(
                message.StoredFileName,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("storage unavailable"));
        var processor = CreateProcessor(dbContext, storage);

        await processor.ProcessPendingMessagesAsync();

        Assert.Null(message.ProcessedAt);
        Assert.Equal(1, message.AttemptCount);
        Assert.Equal("storage unavailable", message.LastError);
        Assert.True(message.NextAttemptAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task ProcessPendingMessages_ignores_messages_that_reached_attempt_limit()
    {
        var options = UnitTestHelper.CreateInMemoryDatabaseOptions($"cleanup-limit-{Guid.NewGuid():N}");
        await using var dbContext = new ApplicationDbContext(options);
        var message = CreateMessage();
        message.AttemptCount = 3;
        message.LastError = "previous failure";
        dbContext.ProjectTaskAttachmentCleanupMessages.Add(message);
        await dbContext.SaveChangesAsync();

        var storage = new Mock<IProjectTaskAttachmentStorage>();
        var processor = CreateProcessor(dbContext, storage);

        await processor.ProcessPendingMessagesAsync();

        storage.Verify(
            service => service.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Equal(3, message.AttemptCount);
        Assert.Equal("previous failure", message.LastError);
        Assert.Null(message.ProcessedAt);
    }

    private static ProjectTaskAttachmentCleanupProcessor CreateProcessor(
        ApplicationDbContext dbContext,
        Mock<IProjectTaskAttachmentStorage> storage)
        => new(
            dbContext,
            storage.Object,
            NullLogger<ProjectTaskAttachmentCleanupProcessor>.Instance);

    private static ProjectTaskAttachmentCleanupMessage CreateMessage()
        => new()
        {
            StoredFileName = $"{Guid.NewGuid():N}.txt",
            NextAttemptAt = DateTime.UtcNow.AddMinutes(-1)
        };
}
