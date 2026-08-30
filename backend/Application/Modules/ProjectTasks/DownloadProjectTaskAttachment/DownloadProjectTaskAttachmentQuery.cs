using Application.Features.Projects;
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
    Guid AttachmentId);

/// <summary>
/// Executes the download-project-task-attachment use case.
/// </summary>
public interface IDownloadProjectTaskAttachmentHandler
{
    Task<ProjectOperationResult<ProjectTaskAttachmentDownload>> HandleAsync(
        DownloadProjectTaskAttachmentQuery query,
        CancellationToken cancellationToken = default);
}

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
