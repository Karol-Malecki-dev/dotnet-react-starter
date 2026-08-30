using Application.Features.Projects;
using Application.Modules.ProjectTasks.Attachments;

namespace Application.Modules.ProjectTasks.ListProjectTaskAttachments;

/// <summary>
/// Represents the application input for listing attachments on an accessible task.
/// </summary>
public sealed record ListProjectTaskAttachmentsQuery(
    Guid UserId,
    Guid ProjectId,
    Guid TaskId);

/// <summary>
/// Executes the list-project-task-attachments use case.
/// </summary>
public interface IListProjectTaskAttachmentsHandler
{
    Task<ProjectOperationResult<IReadOnlyList<ProjectTaskAttachmentView>>> HandleAsync(
        ListProjectTaskAttachmentsQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides the focused persistence operation needed by the list-attachments slice.
/// </summary>
public interface IListProjectTaskAttachmentsQueryStore
{
    Task<IReadOnlyList<ProjectTaskAttachmentView>> QueryAsync(
        Guid taskId,
        CancellationToken cancellationToken = default);
}
