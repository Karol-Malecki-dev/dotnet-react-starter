namespace Application.Modules.ProjectTasks.Comments;

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
