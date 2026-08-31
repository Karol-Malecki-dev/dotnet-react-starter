using Application.Features.ProjectManagement.Tasks;
using Application.Features.Projects;
using Application.Modules.ProjectTasks.Comments;
using Application.Modules.ProjectTasks.CreateProjectTaskComment;
using Domain.Enums;

namespace Infrastructure.Modules.ProjectTasks.CreateProjectTaskComment;

/// <summary>
/// Coordinates validation, authorization, and persistence for creating a task comment.
/// </summary>
public sealed class CreateProjectTaskCommentHandler : ICreateProjectTaskCommentHandler
{
    private readonly IProjectTaskAccess _projectTaskAccess;
    private readonly ICreateProjectTaskCommentStore _commentStore;

    public CreateProjectTaskCommentHandler(
        IProjectTaskAccess projectTaskAccess,
        ICreateProjectTaskCommentStore commentStore)
    {
        _projectTaskAccess = projectTaskAccess;
        _commentStore = commentStore;
    }

    public async Task<ProjectOperationResult<ProjectTaskCommentView>> HandleAsync(
        CreateProjectTaskCommentCommand command,
        CancellationToken cancellationToken = default)
    {
        var content = command.Content.Trim();
        if (string.IsNullOrWhiteSpace(content) || content.Length > 2000)
        {
            return ProjectOperationResult<ProjectTaskCommentView>.Failure(
                ProjectOperationStatus.ValidationError,
                "Comment content must contain between 1 and 2000 characters");
        }

        var role = await _projectTaskAccess.GetActiveProjectRoleAsync(
            command.AuthorUserId,
            command.ProjectId,
            cancellationToken);
        if (role is null)
        {
            return ProjectOperationResult<ProjectTaskCommentView>.Failure(
                ProjectOperationStatus.NotFound,
                "Project task not found");
        }

        var task = await _projectTaskAccess.GetTaskWithLabelsAsync(
            command.ProjectId,
            command.ProjectTaskId,
            cancellationToken);
        if (task is null)
        {
            return ProjectOperationResult<ProjectTaskCommentView>.Failure(
                ProjectOperationStatus.NotFound,
                "Project task not found");
        }

        if (role == ProjectMemberRole.Viewer)
        {
            return ProjectOperationResult<ProjectTaskCommentView>.Failure(
                ProjectOperationStatus.Forbidden,
                "Viewer members cannot add comments");
        }

        var comment = await _commentStore.CreateAsync(
            command with { Content = content },
            cancellationToken);
        return ProjectOperationResult<ProjectTaskCommentView>.Success(
            comment,
            "Project task comment created",
            201);
    }
}
