using Application.Features.Projects;

namespace Application.Features.ProjectManagement.Tasks;

public interface IProjectTaskAttachmentApplicationService
{
    Task<ProjectOperationResult<IReadOnlyList<ProjectTaskAttachmentView>>> GetProjectTaskAttachmentsAsync(
        Guid userId,
        Guid projectId,
        Guid taskId,
        CancellationToken cancellationToken = default);

    Task<ProjectOperationResult<ProjectTaskAttachmentView>> CreateProjectTaskAttachmentAsync(
        CreateProjectTaskAttachmentCommand command,
        CancellationToken cancellationToken = default);

    Task<ProjectOperationResult<ProjectTaskAttachmentDownload>> OpenProjectTaskAttachmentAsync(
        Guid userId,
        Guid projectId,
        Guid taskId,
        Guid attachmentId,
        CancellationToken cancellationToken = default);

    Task<ProjectOperationResult<bool>> DeleteProjectTaskAttachmentAsync(
        Guid userId,
        Guid projectId,
        Guid taskId,
        Guid attachmentId,
        CancellationToken cancellationToken = default);
}