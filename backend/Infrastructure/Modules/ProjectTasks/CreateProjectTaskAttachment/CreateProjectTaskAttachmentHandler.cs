using Application.Features.ProjectManagement.Tasks;
using Application.Features.Projects;
using Application.Modules.ProjectTasks.Attachments;
using Application.Modules.ProjectTasks.CreateProjectTaskAttachment;
using Domain.Enums;

namespace Infrastructure.Modules.ProjectTasks.CreateProjectTaskAttachment;

/// <summary>
/// Coordinates validation, authorization, binary storage, and metadata persistence for uploads.
/// </summary>
public sealed class CreateProjectTaskAttachmentHandler : ICreateProjectTaskAttachmentHandler
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, string> AllowedContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            [".txt"] = "text/plain"
        };

    private readonly IProjectTaskAccess _projectTaskAccess;
    private readonly ICreateProjectTaskAttachmentStore _attachmentStore;
    private readonly IProjectTaskAttachmentStorage _storage;

    public CreateProjectTaskAttachmentHandler(
        IProjectTaskAccess projectTaskAccess,
        ICreateProjectTaskAttachmentStore attachmentStore,
        IProjectTaskAttachmentStorage storage)
    {
        _projectTaskAccess = projectTaskAccess;
        _attachmentStore = attachmentStore;
        _storage = storage;
    }

    public async Task<ProjectOperationResult<ProjectTaskAttachmentView>> HandleAsync(
        CreateProjectTaskAttachmentCommand command,
        CancellationToken cancellationToken = default)
    {
        var role = await _projectTaskAccess.GetActiveProjectRoleAsync(
            command.UserId,
            command.ProjectId,
            cancellationToken);
        if (role is null)
        {
            return ProjectOperationResult<ProjectTaskAttachmentView>.Failure(
                ProjectOperationStatus.NotFound,
                "Project task not found");
        }

        var task = await _projectTaskAccess.GetTaskWithLabelsAsync(
            command.ProjectId,
            command.TaskId,
            cancellationToken);
        if (task is null)
        {
            return ProjectOperationResult<ProjectTaskAttachmentView>.Failure(
                ProjectOperationStatus.NotFound,
                "Project task not found");
        }

        if (role == ProjectMemberRole.Viewer)
        {
            return ProjectOperationResult<ProjectTaskAttachmentView>.Failure(
                ProjectOperationStatus.Forbidden,
                "Viewer members cannot upload attachments");
        }

        var validationError = ValidateFile(
            command.OriginalFileName,
            command.ContentType,
            command.SizeBytes);
        if (validationError is not null)
        {
            return ProjectOperationResult<ProjectTaskAttachmentView>.Failure(
                ProjectOperationStatus.ValidationError,
                validationError);
        }

        var originalFileName = NormalizeFileName(command.OriginalFileName);
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var normalizedCommand = command with
        {
            OriginalFileName = originalFileName,
            ContentType = command.ContentType.Trim()
        };

        try
        {
            await _storage.SaveAsync(command.Content, storedFileName, cancellationToken);
            var attachment = await _attachmentStore.CreateAsync(
                normalizedCommand,
                storedFileName,
                cancellationToken);
            return ProjectOperationResult<ProjectTaskAttachmentView>.Success(
                attachment,
                "Project task attachment created",
                201);
        }
        catch
        {
            await _storage.DeleteAsync(storedFileName, CancellationToken.None);
            throw;
        }
    }

    private static string? ValidateFile(
        string originalFileName,
        string contentType,
        long sizeBytes)
    {
        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            return "A file name is required";
        }

        if (sizeBytes <= 0 || sizeBytes > MaxFileSizeBytes)
        {
            return "Attachment size must be between 1 byte and 10 MB";
        }

        var extension = Path.GetExtension(originalFileName.Trim());
        if (!AllowedContentTypes.TryGetValue(extension, out var expectedContentType)
            || !string.Equals(
                contentType.Trim(),
                expectedContentType,
                StringComparison.OrdinalIgnoreCase))
        {
            return "Attachment format or content type is not allowed";
        }

        var normalizedName = NormalizeFileName(originalFileName);
        return normalizedName.Length > 255
            ? "Attachment file name cannot exceed 255 characters"
            : null;
    }

    private static string NormalizeFileName(string fileName)
        => Path.GetFileName(fileName.Trim().Replace('\\', '/'));
}
