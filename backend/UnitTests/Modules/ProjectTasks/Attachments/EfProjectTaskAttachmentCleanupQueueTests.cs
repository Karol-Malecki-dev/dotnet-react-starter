using Application.Modules.ProjectTasks.Attachments;
using Domain.Entities;
using Infrastructure.Data;
using Infrastructure.Modules.ProjectTasks.Attachments;
using Microsoft.EntityFrameworkCore;
using UnitTests.TestHelpers;

namespace UnitTests.Modules.ProjectTasks.Attachments;

public sealed class EfProjectTaskAttachmentCleanupQueueTests
{
    [Fact]
    public async Task Prepare_task_deletion_stages_metadata_removal_and_returns_each_stored_file_name()
    {
        var options = UnitTestHelper.CreateInMemoryDatabaseOptions($"cleanup-task-delete-{Guid.NewGuid():N}");
        await using var dbContext = new ApplicationDbContext(options);
        var taskId = Guid.NewGuid();
        dbContext.ProjectTaskAttachments.AddRange(
            new ProjectTaskAttachment
            {
                ProjectTaskId = taskId,
                UploadedByUserId = Guid.NewGuid(),
                OriginalFileName = "first.txt",
                StoredFileName = "first-stored.txt",
                ContentType = "text/plain",
                SizeBytes = 1
            },
            new ProjectTaskAttachment
            {
                ProjectTaskId = taskId,
                UploadedByUserId = Guid.NewGuid(),
                OriginalFileName = "second.txt",
                StoredFileName = "second-stored.txt",
                ContentType = "text/plain",
                SizeBytes = 2
            });
        await dbContext.SaveChangesAsync();

        var queue = new EfProjectTaskAttachmentCleanupQueue(dbContext);

        var storedFileNames = await queue.PrepareTaskDeletionAsync(taskId);

        Assert.Equal(
            new[] { "first-stored.txt", "second-stored.txt" },
            storedFileNames.OrderBy(fileName => fileName));
        Assert.Equal(
            2,
            dbContext.ChangeTracker.Entries<ProjectTaskAttachment>()
                .Count(entry => entry.State == EntityState.Deleted));

        await dbContext.SaveChangesAsync();

        Assert.Empty(await dbContext.ProjectTaskAttachments
            .Where(attachment => attachment.ProjectTaskId == taskId)
            .ToListAsync());
    }

    [Fact]
    public async Task Delete_attachment_store_persists_metadata_activity_and_cleanup_message_together()
    {
        var options = UnitTestHelper.CreateInMemoryDatabaseOptions($"cleanup-store-{Guid.NewGuid():N}");
        await using var dbContext = new ApplicationDbContext(options);
        var attachment = new ProjectTaskAttachment
        {
            Id = Guid.NewGuid(),
            ProjectTaskId = Guid.NewGuid(),
            UploadedByUserId = Guid.NewGuid(),
            OriginalFileName = "notes.txt",
            StoredFileName = "stored-notes.txt",
            ContentType = "text/plain",
            SizeBytes = 10,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.ProjectTaskAttachments.Add(attachment);
        await dbContext.SaveChangesAsync();

        var queue = new EfProjectTaskAttachmentCleanupQueue(dbContext);
        var store = new global::Infrastructure.Modules.ProjectTasks.DeleteProjectTaskAttachment.EfDeleteProjectTaskAttachmentStore(
            dbContext,
            queue);

        await store.DeleteAsync(
            attachment,
            Guid.NewGuid(),
            Guid.NewGuid());

        Assert.False(await dbContext.ProjectTaskAttachments.AnyAsync(candidate => candidate.Id == attachment.Id));
        var activity = await dbContext.ProjectActivities.SingleAsync();
        Assert.Equal("task.attachment-removed", activity.Type);
        var cleanupMessage = await dbContext.ProjectTaskAttachmentCleanupMessages.SingleAsync();
        Assert.Equal(attachment.StoredFileName, cleanupMessage.StoredFileName);
        Assert.Null(cleanupMessage.ProcessedAt);
    }
}
