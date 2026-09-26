using Application.Features.Projects;
using MediatR;
using Application.Modules.ProjectTasks.Attachments;

namespace Application.Modules.ProjectTasks.ListProjectTaskAttachments;

/// <summary>
/// Represents the application input for listing attachments on an accessible task.
/// </summary>
public sealed record ListProjectTaskAttachmentsQuery(
    Guid UserId,
    Guid ProjectId,
    Guid TaskId)
    : IRequest<ProjectOperationResult<IReadOnlyList<ProjectTaskAttachmentView>>>;

/// <summary>
/// Provides the focused persistence operation needed by the list-attachments slice.
/// </summary>
public interface IListProjectTaskAttachmentsQueryStore
{
    Task<IReadOnlyList<ProjectTaskAttachmentView>> QueryAsync(
        Guid taskId,
        CancellationToken cancellationToken = default);
}
