namespace Application.Modules.ProjectTasks.Attachments;

/// <summary>
/// Read model for an attachment belonging to a project task.
/// </summary>
public sealed record ProjectTaskAttachmentView(
    Guid Id,
    Guid ProjectTaskId,
    Guid UploadedByUserId,
    string UploaderDisplayName,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    DateTime CreatedAt);

/// <summary>
/// Metadata and content stream returned by the download-attachment use case.
/// </summary>
public sealed record ProjectTaskAttachmentDownload(
    Stream Content,
    string OriginalFileName,
    string ContentType);

/// <summary>
/// Storage port used by attachment slices to manage binary content independently of metadata.
/// </summary>
public interface IProjectTaskAttachmentStorage
{
    Task SaveAsync(
        Stream content,
        string storedFileName,
        CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(
        string storedFileName,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string storedFileName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Optional storage capability used by scheduled orphan reconciliation.
/// Providers that cannot enumerate objects can omit this capability.
/// </summary>
public interface IProjectTaskAttachmentStorageInventory
{
    IAsyncEnumerable<string> EnumerateStoredFileNamesAsync(CancellationToken cancellationToken = default);
}
