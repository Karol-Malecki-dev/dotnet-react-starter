using Application.Features.ProjectManagement.Tasks;
using Application.Features.Projects;
using Application.Modules.ProjectTasks.Attachments;
using Application.Modules.ProjectTasks.DeleteProjectTaskAttachment;
using Domain.Enums;

namespace Infrastructure.Modules.ProjectTasks.DeleteProjectTaskAttachment;

/// <summary>
/// Coordinates authorization, metadata deletion, and binary cleanup for attachments.
/// </summary>
public sealed class DeleteProjectTaskAttachmentHandler : IDeleteProjectTaskAttachmentHandler
{
    private readonly IProjectTaskAccess _projectTaskAccess;
    private readonly IDeleteProjectTaskAttachmentStore _attachmentStore;
    private readonly IProjectTaskAttachmentStorage _storage;

    public DeleteProjectTaskAttachmentHandler(
        IProjectTaskAccess projectTaskAccess,
        IDeleteProjectTaskAttachmentStore attachmentStore,
        IProjectTaskAttachmentStorage storage)
    {
        _projectTaskAccess = projectTaskAccess;
        _attachmentStore = attachmentStore;
        _storage = storage;
    }

    public async Task<ProjectOperationResult<bool>> HandleAsync(
        DeleteProjectTaskAttachmentCommand command,
        CancellationToken cancellationToken = default)
    {
        var role = await _projectTaskAccess.GetActiveProjectRoleAsync(
            command.UserId,
            command.ProjectId,
            cancellationToken);
        if (role is null)
        {
            return ProjectOperationResult<bool>.Failure(
                ProjectOperationStatus.NotFound,
                "Project task not found");
        }

        var task = await _projectTaskAccess.GetTaskWithLabelsAsync(
            command.ProjectId,
            command.TaskId,
            cancellationToken);
        if (task is null)
        {
            return ProjectOperationResult<bool>.Failure(
                ProjectOperationStatus.NotFound,
                "Project task not found");
        }

        var attachment = await _attachmentStore.GetAsync(
            command.TaskId,
            command.AttachmentId,
            cancellationToken);
        if (attachment is null)
        {
            return ProjectOperationResult<bool>.Failure(
                ProjectOperationStatus.NotFound,
                "Project task attachment not found");
        }

        if (role != ProjectMemberRole.Owner
            && attachment.UploadedByUserId != command.UserId)
        {
            return ProjectOperationResult<bool>.Failure(
                ProjectOperationStatus.Forbidden,
                "You cannot delete this attachment");
        }

        await _attachmentStore.DeleteAsync(
            attachment,
            command.ProjectId,
            command.UserId,
            cancellationToken);
        await _storage.DeleteAsync(attachment.StoredFileName, cancellationToken);

        return ProjectOperationResult<bool>.Success(true, "Project task attachment deleted");
    }
}
