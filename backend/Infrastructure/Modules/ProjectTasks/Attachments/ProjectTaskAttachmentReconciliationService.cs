using Application.Modules.ProjectTasks.Attachments;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.ProjectTasks.Attachments;

public sealed record ProjectTaskAttachmentReconciliationReport(
    int MetadataWithoutBinaryCount,
    int BinaryWithoutMetadataCount,
    IReadOnlyList<string> BinaryWithoutMetadata);

/// <summary>
/// Compares durable attachment metadata with an enumerable storage provider.
/// Reconciliation reports differences and does not delete data automatically.
/// </summary>
public sealed class ProjectTaskAttachmentReconciliationService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IProjectTaskAttachmentStorage _storage;

    public ProjectTaskAttachmentReconciliationService(
        ApplicationDbContext dbContext,
        IProjectTaskAttachmentStorage storage)
    {
        _dbContext = dbContext;
        _storage = storage;
    }

    public async Task<ProjectTaskAttachmentReconciliationReport> ReconcileAsync(
        CancellationToken cancellationToken = default)
    {
        if (_storage is not IProjectTaskAttachmentStorageInventory inventory)
        {
            throw new NotSupportedException("The configured attachment storage does not support inventory reconciliation.");
        }

        var metadataFileNames = await _dbContext.ProjectTaskAttachments
            .AsNoTracking()
            .Select(attachment => attachment.StoredFileName)
            .ToHashSetAsync(cancellationToken);
        var binaryFileNames = new HashSet<string>(StringComparer.Ordinal);

        await foreach (var fileName in inventory.EnumerateStoredFileNamesAsync(cancellationToken))
        {
            binaryFileNames.Add(fileName);
        }

        var binariesWithoutMetadata = binaryFileNames
            .Except(metadataFileNames, StringComparer.Ordinal)
            .OrderBy(fileName => fileName, StringComparer.Ordinal)
            .ToArray();

        return new ProjectTaskAttachmentReconciliationReport(
            metadataFileNames.Count(fileName => !binaryFileNames.Contains(fileName)),
            binariesWithoutMetadata.Length,
            binariesWithoutMetadata);
    }
}