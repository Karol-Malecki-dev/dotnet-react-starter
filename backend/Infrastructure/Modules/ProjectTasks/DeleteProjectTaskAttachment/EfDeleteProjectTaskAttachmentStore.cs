using Application.Modules.ProjectTasks.DeleteProjectTaskAttachment;
using Application.Modules.ProjectTasks.Attachments;
using Application.Interfaces;
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
    private readonly ICollaborationNotificationWriter? _notificationWriter;

    public EfDeleteProjectTaskAttachmentStore(
        ApplicationDbContext dbContext,
        IProjectTaskAttachmentCleanupQueue cleanupQueue,
        ICollaborationNotificationWriter? notificationWriter = null)
    {
        _dbContext = dbContext;
        _cleanupQueue = cleanupQueue;
        _notificationWriter = notificationWriter;
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
        if (_notificationWriter is not null)
        {
            var assigneeId = await _dbContext.ProjectTasks
                .Where(task => task.Id == attachment.ProjectTaskId)
                .Select(task => task.AssignedUserId)
                .SingleAsync(cancellationToken);
            if (assigneeId is { } recipientId && recipientId != userId)
            {
                await _notificationWriter.StageAsync(
                    recipientId,
                    Domain.Enums.NotificationType.TaskAttachmentRemoved,
                    "Task attachment removed",
                    $"'{attachment.OriginalFileName}' was removed from your assigned task.",
                    "projectTask",
                    attachment.ProjectTaskId,
                    projectId,
                    $"task:{attachment.ProjectTaskId}:attachment:{attachment.Id}:removed",
                    cancellationToken);
            }
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
