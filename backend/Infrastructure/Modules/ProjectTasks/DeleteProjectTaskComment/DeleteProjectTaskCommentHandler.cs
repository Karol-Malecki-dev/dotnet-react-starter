using Application.Features.ProjectManagement.Tasks;
using Application.Features.Projects;
using Application.Modules.ProjectTasks.DeleteProjectTaskComment;
using Domain.Enums;

namespace Infrastructure.Modules.ProjectTasks.DeleteProjectTaskComment;

/// <summary>
/// Coordinates authorization and persistence for deleting a task comment.
/// </summary>
public sealed class DeleteProjectTaskCommentHandler : IDeleteProjectTaskCommentHandler
{
    private readonly IProjectTaskAccess _projectTaskAccess;
    private readonly IDeleteProjectTaskCommentStore _commentStore;

    public DeleteProjectTaskCommentHandler(
        IProjectTaskAccess projectTaskAccess,
        IDeleteProjectTaskCommentStore commentStore)
    {
        _projectTaskAccess = projectTaskAccess;
        _commentStore = commentStore;
    }

    public async Task<ProjectOperationResult<bool>> HandleAsync(
        DeleteProjectTaskCommentCommand command,
        CancellationToken cancellationToken = default)
    {
        var role = await _projectTaskAccess.GetActiveProjectRoleAsync(
            command.UserId,
            command.ProjectId,
            cancellationToken);
        if (role is null)
        {
            return ProjectOperationResult<bool>.Failure(
                ProjectOperationStatus.NotFound,
                "Project task not found");
        }

        var task = await _projectTaskAccess.GetTaskWithLabelsAsync(
            command.ProjectId,
            command.ProjectTaskId,
            cancellationToken);
        if (task is null)
        {
            return ProjectOperationResult<bool>.Failure(
                ProjectOperationStatus.NotFound,
                "Project task not found");
        }

        var comment = await _commentStore.GetAsync(
            command.ProjectTaskId,
            command.CommentId,
            cancellationToken);
        if (comment is null)
        {
            return ProjectOperationResult<bool>.Failure(
                ProjectOperationStatus.NotFound,
                "Project task comment not found");
        }

        if (role != ProjectMemberRole.Owner && comment.AuthorUserId != command.UserId)
        {
            return ProjectOperationResult<bool>.Failure(
                ProjectOperationStatus.Forbidden,
                "You cannot delete this comment");
        }

        _commentStore.Remove(comment);
        await _commentStore.SaveChangesAsync(cancellationToken);
        return ProjectOperationResult<bool>.Success(true, "Project task comment deleted");
    }
}
