using Application.Modules.ProjectTasks.Attachments;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Modules.ProjectTasks.Attachments;

/// <summary>
/// Removes attachment binaries and persists retry state for failures.
/// </summary>
public sealed class ProjectTaskAttachmentCleanupProcessor : IProjectTaskAttachmentCleanupProcessor
{
    private const int MaxAttempts = 3;
    private const int BatchSize = 20;

    private readonly ApplicationDbContext _dbContext;
    private readonly IProjectTaskAttachmentStorage _storage;
    private readonly ILogger<ProjectTaskAttachmentCleanupProcessor> _logger;

    public ProjectTaskAttachmentCleanupProcessor(
        ApplicationDbContext dbContext,
        IProjectTaskAttachmentStorage storage,
        ILogger<ProjectTaskAttachmentCleanupProcessor> logger)
    {
        _dbContext = dbContext;
        _storage = storage;
        _logger = logger;
    }

    public async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var messages = await _dbContext.ProjectTaskAttachmentCleanupMessages
            .Where(message => message.ProcessedAt == null
                && message.AttemptCount < MaxAttempts
                && message.NextAttemptAt <= now)
            .OrderBy(message => message.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                await _storage.DeleteAsync(message.StoredFileName, cancellationToken);
                message.ProcessedAt = DateTime.UtcNow;
                message.LastError = null;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                message.AttemptCount += 1;
                message.LastError = exception.Message[..Math.Min(exception.Message.Length, 2000)];
                message.NextAttemptAt = DateTime.UtcNow.AddMinutes(message.AttemptCount);
                _logger.LogWarning(
                    exception,
                    "Project task attachment cleanup failed for message {CleanupMessageId}",
                    message.Id);
            }
        }

        if (messages.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
