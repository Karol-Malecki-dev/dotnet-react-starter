using Application.Features.Projects;
using Domain.Entities;

namespace Application.Modules.ProjectTasks.DeleteProjectTaskAttachment;

/// <summary>
/// Represents the application input for deleting a project task attachment.
/// </summary>
public sealed record DeleteProjectTaskAttachmentCommand(
    Guid UserId,
    Guid ProjectId,
    Guid TaskId,
    Guid AttachmentId);

/// <summary>
/// Executes the delete-project-task-attachment use case.
/// </summary>
public interface IDeleteProjectTaskAttachmentHandler
{
    Task<ProjectOperationResult<bool>> HandleAsync(
        DeleteProjectTaskAttachmentCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides the focused metadata operations needed by the delete-attachment slice.
/// </summary>
public interface IDeleteProjectTaskAttachmentStore
{
    Task<ProjectTaskAttachment?> GetAsync(
        Guid taskId,
        Guid attachmentId,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        ProjectTaskAttachment attachment,
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
