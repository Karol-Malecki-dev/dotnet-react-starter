using Application.Modules.ProjectTasks.Attachments;
using Domain.Entities;
using Infrastructure.Data;
using Infrastructure.Modules.ProjectTasks.Attachments;
using Microsoft.EntityFrameworkCore;

namespace UnitTests.Modules.ProjectTasks.Attachments;

public sealed class ProjectTaskAttachmentReconciliationServiceTests
{
    [Fact]
    public async Task Reconcile_reports_missing_binary_and_orphan_binary_without_deleting_data()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nameof(Reconcile_reports_missing_binary_and_orphan_binary_without_deleting_data))
            .Options;
        await using var context = new ApplicationDbContext(options);
        var metadataFileName = $"{Guid.NewGuid():N}.pdf";
        var orphanFileName = $"{Guid.NewGuid():N}.png";
        context.ProjectTaskAttachments.Add(new ProjectTaskAttachment
        {
            ProjectTaskId = Guid.NewGuid(),
            UploadedByUserId = Guid.NewGuid(),
            OriginalFileName = "document.pdf",
            StoredFileName = metadataFileName,
            ContentType = "application/pdf",
            SizeBytes = 10
        });
        await context.SaveChangesAsync();
        var storage = new InventoryStorage([orphanFileName]);
        var service = new ProjectTaskAttachmentReconciliationService(context, storage);

        var report = await service.ReconcileAsync();

        Assert.Equal(1, report.MetadataWithoutBinaryCount);
        Assert.Equal(1, report.BinaryWithoutMetadataCount);
        Assert.Equal([orphanFileName], report.BinaryWithoutMetadata);
        Assert.Empty(storage.DeletedFileNames);
    }

    private sealed class InventoryStorage(IReadOnlyList<string> fileNames)
        : IProjectTaskAttachmentStorage, IProjectTaskAttachmentStorageInventory
    {
        public List<string> DeletedFileNames { get; } = [];

        public async IAsyncEnumerable<string> EnumerateStoredFileNamesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var fileName in fileNames)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return fileName;
                await Task.Yield();
            }
        }

        public Task SaveAsync(Stream content, string storedFileName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<Stream?> OpenReadAsync(string storedFileName, CancellationToken cancellationToken = default)
            => Task.FromResult<Stream?>(null);

        public Task DeleteAsync(string storedFileName, CancellationToken cancellationToken = default)
        {
            DeletedFileNames.Add(storedFileName);
            return Task.CompletedTask;
        }
    }
}