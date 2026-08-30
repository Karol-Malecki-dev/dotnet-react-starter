using Application.Features.Projects;
using Application.Modules.ProjectTasks.Comments;

namespace Application.Modules.ProjectTasks.CreateProjectTaskComment;

/// <summary>
/// Represents the application input for adding a comment to a project task.
/// </summary>
public sealed record CreateProjectTaskCommentCommand(
    Guid AuthorUserId,
    Guid ProjectId,
    Guid ProjectTaskId,
    string Content);

/// <summary>
/// Executes the create-project-task-comment use case.
/// </summary>
public interface ICreateProjectTaskCommentHandler
{
    Task<ProjectOperationResult<ProjectTaskCommentView>> HandleAsync(
        CreateProjectTaskCommentCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides the focused persistence operation needed by the create-comment slice.
/// </summary>
public interface ICreateProjectTaskCommentStore
{
    Task<ProjectTaskCommentView> CreateAsync(
        CreateProjectTaskCommentCommand command,
        CancellationToken cancellationToken = default);
}
