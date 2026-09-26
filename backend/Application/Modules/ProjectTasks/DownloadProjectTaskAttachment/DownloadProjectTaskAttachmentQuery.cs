using Application.Features.Projects;
using MediatR;
using Application.Modules.ProjectTasks.Attachments;
using Domain.Entities;

namespace Application.Modules.ProjectTasks.DownloadProjectTaskAttachment;

/// <summary>
/// Represents the application input for downloading a project task attachment.
/// </summary>
public sealed record DownloadProjectTaskAttachmentQuery(
    Guid UserId,
    Guid ProjectId,
    Guid TaskId,
    Guid AttachmentId)
    : IRequest<ProjectOperationResult<ProjectTaskAttachmentDownload>>;



/// <summary>
/// Provides the focused metadata lookup needed by the download-attachment slice.
/// </summary>
public interface IDownloadProjectTaskAttachmentStore
{
    Task<ProjectTaskAttachment?> GetAsync(
        Guid taskId,
        Guid attachmentId,
        CancellationToken cancellationToken = default);
}
