namespace Application.Features.Projects;

/// <summary>
/// Read model for a comment displayed in a project task discussion.
/// </summary>
public sealed record ProjectTaskCommentView(
    Guid Id,
    Guid ProjectTaskId,
    Guid AuthorUserId,
    string AuthorDisplayName,
    string Content,
    DateTime CreatedAt);

/// <summary>
/// Command to add a comment to an accessible, active project task.
/// </summary>
public sealed record CreateProjectTaskCommentCommand(
    Guid AuthorUserId,
    Guid ProjectId,
    Guid ProjectTaskId,
    string Content);
