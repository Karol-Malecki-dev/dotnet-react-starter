using Application.Features.Projects;

namespace Application.Features.ProjectManagement.Tasks;

public interface IProjectTaskAttachmentApplicationService
{
    Task<ProjectOperationResult<IReadOnlyList<ProjectTaskAttachmentView>>> GetProjectTaskAttachmentsAsync(
        Guid userId,
        Guid projectId,
        Guid taskId);

    Task<ProjectOperationResult<ProjectTaskAttachmentView>> CreateProjectTaskAttachmentAsync(
        CreateProjectTaskAttachmentCommand command);

    Task<ProjectOperationResult<ProjectTaskAttachmentDownload>> OpenProjectTaskAttachmentAsync(
        Guid userId,
        Guid projectId,
        Guid taskId,
        Guid attachmentId);

    Task<ProjectOperationResult<bool>> DeleteProjectTaskAttachmentAsync(
        Guid userId,
        Guid projectId,
        Guid taskId,
        Guid attachmentId);
}