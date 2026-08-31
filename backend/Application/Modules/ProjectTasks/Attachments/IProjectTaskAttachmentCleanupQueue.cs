namespace Application.Modules.ProjectTasks.Attachments;

/// <summary>
/// Stages durable cleanup messages for physical task attachment files.
/// </summary>
public interface IProjectTaskAttachmentCleanupQueue
{
    /// <summary>
    /// Stages task attachment metadata for deletion and returns the file names
    /// that must be removed from physical storage.
    /// </summary>
    Task<IReadOnlyList<string>> PrepareTaskDeletionAsync(
        Guid projectTaskId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a cleanup message to the current persistence unit of work.
    /// </summary>
    void Enqueue(string storedFileName);
}

/// <summary>
/// Processes durable task attachment cleanup messages.
/// </summary>
public interface IProjectTaskAttachmentCleanupProcessor
{
    Task ProcessPendingMessagesAsync(CancellationToken cancellationToken = default);
}
