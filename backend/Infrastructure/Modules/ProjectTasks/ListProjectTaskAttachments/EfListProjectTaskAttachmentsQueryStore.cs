using Application.Modules.ProjectTasks.Attachments;
using Application.Modules.ProjectTasks.ListProjectTaskAttachments;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.ProjectTasks.ListProjectTaskAttachments;

/// <summary>
/// EF Core implementation of the list-attachments persistence port.
/// </summary>
public sealed class EfListProjectTaskAttachmentsQueryStore : IListProjectTaskAttachmentsQueryStore
{
    private readonly ApplicationDbContext _dbContext;

    public EfListProjectTaskAttachmentsQueryStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ProjectTaskAttachmentView>> QueryAsync(
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        var attachments = await _dbContext.ProjectTaskAttachments
            .AsNoTracking()
            .Where(attachment => attachment.ProjectTaskId == taskId)
            .OrderByDescending(attachment => attachment.CreatedAt)
            .Select(attachment => new
            {
                attachment.Id,
                attachment.ProjectTaskId,
                attachment.UploadedByUserId,
                UploaderDisplayName = attachment.UploadedByUser.DisplayName,
                attachment.OriginalFileName,
                attachment.ContentType,
                attachment.SizeBytes,
                attachment.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return attachments
            .Select(attachment => new ProjectTaskAttachmentView(
                attachment.Id,
                attachment.ProjectTaskId,
                attachment.UploadedByUserId,
                attachment.UploaderDisplayName.Value,
                attachment.OriginalFileName,
                attachment.ContentType,
                attachment.SizeBytes,
                attachment.CreatedAt))
            .ToList();
    }
}
