using Application.Modules.ProjectTasks.DownloadProjectTaskAttachment;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Modules.ProjectTasks.DownloadProjectTaskAttachment;

/// <summary>
/// EF Core implementation of the download-attachment metadata lookup.
/// </summary>
public sealed class EfDownloadProjectTaskAttachmentStore : IDownloadProjectTaskAttachmentStore
{
    private readonly ApplicationDbContext _dbContext;

    public EfDownloadProjectTaskAttachmentStore(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<ProjectTaskAttachment?> GetAsync(
        Guid taskId,
        Guid attachmentId,
        CancellationToken cancellationToken = default)
        => _dbContext.ProjectTaskAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                attachment => attachment.Id == attachmentId
                    && attachment.ProjectTaskId == taskId,
                cancellationToken);
}
