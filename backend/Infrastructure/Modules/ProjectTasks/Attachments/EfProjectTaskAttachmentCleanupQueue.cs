using Application.Modules.ProjectTasks.Attachments;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.ProjectTasks.Attachments;

/// <summary>
/// EF Core adapter for staging attachment cleanup messages in the current unit of work.
/// </summary>
public sealed class EfProjectTaskAttachmentCleanupQueue : IProjectTaskAttachmentCleanupQueue
{
    private readonly ApplicationDbContext _dbContext;

    public EfProjectTaskAttachmentCleanupQueue(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<string>> PrepareTaskDeletionAsync(
        Guid projectTaskId,
        CancellationToken cancellationToken = default)
    {
        var attachments = await _dbContext.ProjectTaskAttachments
            .Where(attachment => attachment.ProjectTaskId == projectTaskId)
            .ToListAsync(cancellationToken);

        _dbContext.ProjectTaskAttachments.RemoveRange(attachments);
        return attachments
            .Select(attachment => attachment.StoredFileName)
            .ToList();
    }

    public void Enqueue(string storedFileName)
    {
        if (string.IsNullOrWhiteSpace(storedFileName))
        {
            throw new ArgumentException("A stored file name is required.", nameof(storedFileName));
        }

        _dbContext.ProjectTaskAttachmentCleanupMessages.Add(new ProjectTaskAttachmentCleanupMessage
        {
            StoredFileName = storedFileName
        });
    }
}
