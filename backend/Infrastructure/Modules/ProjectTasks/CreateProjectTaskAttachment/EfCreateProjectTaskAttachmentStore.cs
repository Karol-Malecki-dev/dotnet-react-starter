using Application.Modules.ProjectTasks.Attachments;
using Application.Modules.ProjectTasks.CreateProjectTaskAttachment;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.ProjectTasks.CreateProjectTaskAttachment;

/// <summary>
/// EF Core implementation of the create-attachment persistence port.
/// </summary>
public sealed class EfCreateProjectTaskAttachmentStore : ICreateProjectTaskAttachmentStore
{
    private readonly ApplicationDbContext _dbContext;

    public EfCreateProjectTaskAttachmentStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProjectTaskAttachmentView> CreateAsync(
        CreateProjectTaskAttachmentCommand command,
        string storedFileName,
        CancellationToken cancellationToken = default)
    {
        var attachment = new ProjectTaskAttachment
        {
            ProjectTaskId = command.TaskId,
            UploadedByUserId = command.UserId,
            OriginalFileName = command.OriginalFileName,
            StoredFileName = storedFileName,
            ContentType = command.ContentType,
            SizeBytes = command.SizeBytes
        };
        _dbContext.ProjectTaskAttachments.Add(attachment);
        _dbContext.ProjectActivities.Add(new ProjectActivity
        {
            ProjectId = command.ProjectId,
            ActorUserId = command.UserId,
            ProjectTaskId = command.TaskId,
            Type = "task.attachment-added",
            Description = $"added the attachment '{command.OriginalFileName}'."
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        var uploaderDisplayName = await _dbContext.Users
            .Where(user => user.Id == command.UserId)
            .Select(user => user.DisplayName)
            .SingleAsync(cancellationToken);

        return new ProjectTaskAttachmentView(
            attachment.Id,
            attachment.ProjectTaskId,
            attachment.UploadedByUserId,
            uploaderDisplayName.Value,
            attachment.OriginalFileName,
            attachment.ContentType,
            attachment.SizeBytes,
            attachment.CreatedAt);
    }
}
