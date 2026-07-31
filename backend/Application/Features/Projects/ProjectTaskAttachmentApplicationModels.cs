namespace Application.Features.Projects;

public sealed record ProjectTaskAttachmentView(
    Guid Id,
    Guid ProjectTaskId,
    Guid UploadedByUserId,
    string UploaderDisplayName,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    DateTime CreatedAt);

public sealed record CreateProjectTaskAttachmentCommand(
    Guid UserId,
    Guid ProjectId,
    Guid TaskId,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    Stream Content);

public sealed record ProjectTaskAttachmentDownload(
    Stream Content,
    string OriginalFileName,
    string ContentType);