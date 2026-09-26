using Application.Features.Projects;
using MediatR;
using Application.Modules.ProjectTasks.Attachments;

namespace Application.Modules.ProjectTasks.CreateProjectTaskAttachment;

/// <summary>
/// Represents the application input for uploading an attachment to a project task.
/// </summary>
public sealed record CreateProjectTaskAttachmentCommand(
    Guid UserId,
    Guid ProjectId,
    Guid TaskId,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    Stream Content)
    : IRequest<ProjectOperationResult<ProjectTaskAttachmentView>>;



/// <summary>
/// Provides the focused persistence operation needed by the create-attachment slice.
/// </summary>
public interface ICreateProjectTaskAttachmentStore
{
    Task<ProjectTaskAttachmentView> CreateAsync(
        CreateProjectTaskAttachmentCommand command,
        string storedFileName,
        CancellationToken cancellationToken = default);
}
