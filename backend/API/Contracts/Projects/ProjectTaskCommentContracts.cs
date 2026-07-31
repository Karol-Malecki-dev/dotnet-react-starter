namespace API.Contracts.Projects;

/// <summary>
/// Request payload for adding a discussion comment to a project task.
/// </summary>
/// <param name="Content">Non-empty comment text, up to 2000 characters after trimming.</param>
public sealed record CreateProjectTaskCommentRequest(string Content);

/// <summary>
/// Response payload representing one comment in a project task discussion.
/// </summary>
public sealed record ProjectTaskCommentResponse(
    Guid Id,
    Guid ProjectTaskId,
    Guid AuthorUserId,
    string AuthorDisplayName,
    string Content,
    DateTime CreatedAt);
