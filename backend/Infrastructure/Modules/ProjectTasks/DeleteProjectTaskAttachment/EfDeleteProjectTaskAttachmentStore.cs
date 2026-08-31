using Application.Modules.ProjectTasks.DeleteProjectTaskAttachment;
using Application.Modules.ProjectTasks.Attachments;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.ProjectTasks.DeleteProjectTaskAttachment;

/// <summary>
/// EF Core implementation of attachment metadata deletion and activity recording.
/// </summary>
public sealed class EfDeleteProjectTaskAttachmentStore : IDeleteProjectTaskAttachmentStore
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IProjectTaskAttachmentCleanupQueue _cleanupQueue;

    public EfDeleteProjectTaskAttachmentStore(
        ApplicationDbContext dbContext,
        IProjectTaskAttachmentCleanupQueue cleanupQueue)
    {
        _dbContext = dbContext;
        _cleanupQueue = cleanupQueue;
    }

    public Task<ProjectTaskAttachment?> GetAsync(
        Guid taskId,
        Guid attachmentId,
        CancellationToken cancellationToken = default)
        => _dbContext.ProjectTaskAttachments
            .FirstOrDefaultAsync(
                attachment => attachment.Id == attachmentId
                    && attachment.ProjectTaskId == taskId,
                cancellationToken);

    public async Task DeleteAsync(
        ProjectTaskAttachment attachment,
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        _dbContext.ProjectTaskAttachments.Remove(attachment);
        _dbContext.ProjectActivities.Add(new ProjectActivity
        {
            ProjectId = projectId,
            ActorUserId = userId,
            ProjectTaskId = attachment.ProjectTaskId,
            Type = "task.attachment-removed",
            Description = $"removed the attachment '{attachment.OriginalFileName}'."
        });
        _cleanupQueue.Enqueue(attachment.StoredFileName);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
