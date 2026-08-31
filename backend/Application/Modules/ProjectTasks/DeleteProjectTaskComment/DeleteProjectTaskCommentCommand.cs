using Application.Features.Projects;
using Domain.Entities;

namespace Application.Modules.ProjectTasks.DeleteProjectTaskComment;

/// <summary>
/// Represents the application input for deleting a project task comment.
/// </summary>
public sealed record DeleteProjectTaskCommentCommand(
    Guid UserId,
    Guid ProjectId,
    Guid ProjectTaskId,
    Guid CommentId);

/// <summary>
/// Executes the delete-project-task-comment use case.
/// </summary>
public interface IDeleteProjectTaskCommentHandler
{
    Task<ProjectOperationResult<bool>> HandleAsync(
        DeleteProjectTaskCommentCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides the focused persistence operations needed by the delete-comment slice.
/// </summary>
public interface IDeleteProjectTaskCommentStore
{
    Task<ProjectTaskComment?> GetAsync(
        Guid taskId,
        Guid commentId,
        CancellationToken cancellationToken = default);

    void Remove(ProjectTaskComment comment);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
