using Application.Features.ProjectManagement.Tasks;
using Application.Features.Projects;
using Application.Modules.ProjectTasks.Attachments;
using Application.Modules.ProjectTasks.DownloadProjectTaskAttachment;

namespace Infrastructure.Modules.ProjectTasks.DownloadProjectTaskAttachment;

/// <summary>
/// Coordinates access checks, metadata lookup, and binary stream access for downloads.
/// </summary>
public sealed class DownloadProjectTaskAttachmentHandler : IDownloadProjectTaskAttachmentHandler
{
    private readonly IProjectTaskAccess _projectTaskAccess;
    private readonly IDownloadProjectTaskAttachmentStore _attachmentStore;
    private readonly IProjectTaskAttachmentStorage _storage;

    public DownloadProjectTaskAttachmentHandler(
        IProjectTaskAccess projectTaskAccess,
        IDownloadProjectTaskAttachmentStore attachmentStore,
        IProjectTaskAttachmentStorage storage)
    {
        _projectTaskAccess = projectTaskAccess;
        _attachmentStore = attachmentStore;
        _storage = storage;
    }

    public async Task<ProjectOperationResult<ProjectTaskAttachmentDownload>> HandleAsync(
        DownloadProjectTaskAttachmentQuery query,
        CancellationToken cancellationToken = default)
    {
        var role = await _projectTaskAccess.GetActiveProjectRoleAsync(
            query.UserId,
            query.ProjectId,
            cancellationToken);
        if (role is null)
        {
            return ProjectOperationResult<ProjectTaskAttachmentDownload>.Failure(
                ProjectOperationStatus.NotFound,
                "Project task not found");
        }

        var task = await _projectTaskAccess.GetTaskWithLabelsAsync(
            query.ProjectId,
            query.TaskId,
            cancellationToken);
        if (task is null)
        {
            return ProjectOperationResult<ProjectTaskAttachmentDownload>.Failure(
                ProjectOperationStatus.NotFound,
                "Project task not found");
        }

        var attachment = await _attachmentStore.GetAsync(
            query.TaskId,
            query.AttachmentId,
            cancellationToken);
        if (attachment is null)
        {
            return ProjectOperationResult<ProjectTaskAttachmentDownload>.Failure(
                ProjectOperationStatus.NotFound,
                "Project task attachment not found");
        }

        var stream = await _storage.OpenReadAsync(
            attachment.StoredFileName,
            cancellationToken);
        if (stream is null)
        {
            return ProjectOperationResult<ProjectTaskAttachmentDownload>.Failure(
                ProjectOperationStatus.NotFound,
                "Project task attachment file not found");
        }

        return ProjectOperationResult<ProjectTaskAttachmentDownload>.Success(
            new ProjectTaskAttachmentDownload(
                stream,
                attachment.OriginalFileName,
                attachment.ContentType));
    }
}
