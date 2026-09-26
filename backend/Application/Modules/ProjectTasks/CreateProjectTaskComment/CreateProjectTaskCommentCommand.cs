using Application.Features.Projects;
using MediatR;
using Application.Modules.ProjectTasks.Comments;

namespace Application.Modules.ProjectTasks.CreateProjectTaskComment;

/// <summary>
/// Represents the application input for adding a comment to a project task.
/// </summary>
public sealed record CreateProjectTaskCommentCommand(
    Guid AuthorUserId,
    Guid ProjectId,
    Guid ProjectTaskId,
    string Content)
    : IRequest<ProjectOperationResult<ProjectTaskCommentView>>;



/// <summary>
/// Provides the focused persistence operation needed by the create-comment slice.
/// </summary>
public interface ICreateProjectTaskCommentStore
{
    Task<ProjectTaskCommentView> CreateAsync(
        CreateProjectTaskCommentCommand command,
        CancellationToken cancellationToken = default);
}
