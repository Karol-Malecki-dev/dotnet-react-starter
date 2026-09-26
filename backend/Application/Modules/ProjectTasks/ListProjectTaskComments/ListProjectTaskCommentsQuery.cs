using Application.Features.Projects;
using MediatR;
using Application.Modules.ProjectTasks.Comments;

namespace Application.Modules.ProjectTasks.ListProjectTaskComments;

/// <summary>
/// Represents the application input for listing comments on an accessible project task.
/// </summary>
public sealed record ListProjectTaskCommentsQuery(
    Guid UserId,
    Guid ProjectId,
    Guid TaskId)
    : IRequest<ProjectOperationResult<IReadOnlyList<ProjectTaskCommentView>>>;

/// <summary>
/// Provides the focused persistence operation needed by the list-comments slice.
/// </summary>
public interface IListProjectTaskCommentsQueryStore
{
    Task<IReadOnlyList<ProjectTaskCommentView>> QueryAsync(
        Guid taskId,
        CancellationToken cancellationToken = default);
}
