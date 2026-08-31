using Application.Features.Projects;
using Application.Modules.ProjectTasks.Comments;

namespace Application.Modules.ProjectTasks.ListProjectTaskComments;

/// <summary>
/// Represents the application input for listing comments on an accessible project task.
/// </summary>
public sealed record ListProjectTaskCommentsQuery(
    Guid UserId,
    Guid ProjectId,
    Guid TaskId);

/// <summary>
/// Executes the list-project-task-comments use case.
/// </summary>
public interface IListProjectTaskCommentsHandler
{
    Task<ProjectOperationResult<IReadOnlyList<ProjectTaskCommentView>>> HandleAsync(
        ListProjectTaskCommentsQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides the focused persistence operation needed by the list-comments slice.
/// </summary>
public interface IListProjectTaskCommentsQueryStore
{
    Task<IReadOnlyList<ProjectTaskCommentView>> QueryAsync(
        Guid taskId,
        CancellationToken cancellationToken = default);
}
