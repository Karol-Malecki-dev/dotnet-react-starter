namespace API.Contracts.Projects;

/// <summary>
/// Response payload representing one attachment belonging to a project task.
/// </summary>
public sealed record ProjectTaskAttachmentResponse(
    Guid Id,
    Guid ProjectTaskId,
    Guid UploadedByUserId,
    string UploaderDisplayName,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    DateTime CreatedAt);