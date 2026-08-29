using Application.Features.Projects;
using Application.Features.ProjectManagement.Tasks;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class DatabaseProjectTaskAttachmentService : IProjectTaskAttachmentApplicationService
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, string> AllowedContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".txt"] = "text/plain"
    };

    private readonly ApplicationDbContext _dbContext;
    private readonly IProjectTaskAttachmentStorage _storage;

    public DatabaseProjectTaskAttachmentService(
        ApplicationDbContext dbContext,
        IProjectTaskAttachmentStorage storage)
    {
        _dbContext = dbContext;
        _storage = storage;
    }

    public async Task<ProjectOperationResult<IReadOnlyList<ProjectTaskAttachmentView>>> GetProjectTaskAttachmentsAsync(
        Guid userId,
        Guid projectId,
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        if (!await HasProjectAccessAsync(userId, projectId, cancellationToken) || !await TaskBelongsToProjectAsync(projectId, taskId, cancellationToken))
        {
            return ProjectOperationResult<IReadOnlyList<ProjectTaskAttachmentView>>.Failure(
                ProjectOperationStatus.NotFound,
                "Project task not found");
        }

        var attachments = await _dbContext.ProjectTaskAttachments
            .AsNoTracking()
            .Where(attachment => attachment.ProjectTaskId == taskId)
            .OrderByDescending(attachment => attachment.CreatedAt)
            .Select(attachment => new
            {
                attachment.Id,
                attachment.ProjectTaskId,
                attachment.UploadedByUserId,
                UploaderDisplayName = attachment.UploadedByUser.DisplayName,
                attachment.OriginalFileName,
                attachment.ContentType,
                attachment.SizeBytes,
                attachment.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var attachmentViews = attachments
            .Select(attachment => new ProjectTaskAttachmentView(
                attachment.Id,
                attachment.ProjectTaskId,
                attachment.UploadedByUserId,
                attachment.UploaderDisplayName.Value,
                attachment.OriginalFileName,
                attachment.ContentType,
                attachment.SizeBytes,
                attachment.CreatedAt))
            .ToList();

        return ProjectOperationResult<IReadOnlyList<ProjectTaskAttachmentView>>.Success(attachmentViews);
    }

    public async Task<ProjectOperationResult<ProjectTaskAttachmentView>> CreateProjectTaskAttachmentAsync(
        CreateProjectTaskAttachmentCommand command,
        CancellationToken cancellationToken = default)
    {
        var role = await GetProjectRoleAsync(command.UserId, command.ProjectId, cancellationToken);
        if (role is null || !await TaskBelongsToProjectAsync(command.ProjectId, command.TaskId, cancellationToken))
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

        var validationError = ValidateFile(command.OriginalFileName, command.ContentType, command.SizeBytes);
        if (validationError is not null)
        {
            return ProjectOperationResult<ProjectTaskAttachmentView>.Failure(
                ProjectOperationStatus.ValidationError,
                validationError);
        }

        var originalFileName = Path.GetFileName(command.OriginalFileName.Trim());
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var attachment = new ProjectTaskAttachment
        {
            ProjectTaskId = command.TaskId,
            UploadedByUserId = command.UserId,
            OriginalFileName = originalFileName,
            StoredFileName = storedFileName,
            ContentType = command.ContentType.Trim(),
            SizeBytes = command.SizeBytes
        };

        try
        {
            await _storage.SaveAsync(command.Content, storedFileName, cancellationToken);
            _dbContext.ProjectTaskAttachments.Add(attachment);
            _dbContext.ProjectActivities.Add(new ProjectActivity
            {
                ProjectId = command.ProjectId,
                ActorUserId = command.UserId,
                ProjectTaskId = command.TaskId,
                Type = "task.attachment-added",
                Description = $"added the attachment '{originalFileName}'."
            });
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await _storage.DeleteAsync(storedFileName, CancellationToken.None);
            throw;
        }

        var uploaderDisplayName = await _dbContext.Users
            .Where(user => user.Id == command.UserId)
            .Select(user => user.DisplayName)
            .SingleAsync(cancellationToken);

        return ProjectOperationResult<ProjectTaskAttachmentView>.Success(
            new ProjectTaskAttachmentView(
                attachment.Id,
                attachment.ProjectTaskId,
                attachment.UploadedByUserId,
                uploaderDisplayName.Value,
                attachment.OriginalFileName,
                attachment.ContentType,
                attachment.SizeBytes,
                attachment.CreatedAt),
            "Project task attachment created",
            201);
    }

    public async Task<ProjectOperationResult<ProjectTaskAttachmentDownload>> OpenProjectTaskAttachmentAsync(
        Guid userId,
        Guid projectId,
        Guid taskId,
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        if (!await HasProjectAccessAsync(userId, projectId, cancellationToken) || !await TaskBelongsToProjectAsync(projectId, taskId, cancellationToken))
        {
            return ProjectOperationResult<ProjectTaskAttachmentDownload>.Failure(ProjectOperationStatus.NotFound, "Project task not found");
        }

        var attachment = await _dbContext.ProjectTaskAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == attachmentId && candidate.ProjectTaskId == taskId, cancellationToken);
        if (attachment is null)
        {
            return ProjectOperationResult<ProjectTaskAttachmentDownload>.Failure(ProjectOperationStatus.NotFound, "Project task attachment not found");
        }

        var stream = await _storage.OpenReadAsync(attachment.StoredFileName, cancellationToken);
        return stream is null
            ? ProjectOperationResult<ProjectTaskAttachmentDownload>.Failure(ProjectOperationStatus.NotFound, "Project task attachment file not found")
            : ProjectOperationResult<ProjectTaskAttachmentDownload>.Success(
                new ProjectTaskAttachmentDownload(stream, attachment.OriginalFileName, attachment.ContentType));
    }

    public async Task<ProjectOperationResult<bool>> DeleteProjectTaskAttachmentAsync(
        Guid userId,
        Guid projectId,
        Guid taskId,
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        var role = await GetProjectRoleAsync(userId, projectId, cancellationToken);
        if (role is null || !await TaskBelongsToProjectAsync(projectId, taskId, cancellationToken))
        {
            return ProjectOperationResult<bool>.Failure(ProjectOperationStatus.NotFound, "Project task not found");
        }

        var attachment = await _dbContext.ProjectTaskAttachments
            .FirstOrDefaultAsync(candidate => candidate.Id == attachmentId && candidate.ProjectTaskId == taskId, cancellationToken);
        if (attachment is null)
        {
            return ProjectOperationResult<bool>.Failure(ProjectOperationStatus.NotFound, "Project task attachment not found");
        }

        if (role != ProjectMemberRole.Owner && attachment.UploadedByUserId != userId)
        {
            return ProjectOperationResult<bool>.Failure(ProjectOperationStatus.Forbidden, "You cannot delete this attachment");
        }

        _dbContext.ProjectTaskAttachments.Remove(attachment);
        _dbContext.ProjectActivities.Add(new ProjectActivity
        {
            ProjectId = projectId,
            ActorUserId = userId,
            ProjectTaskId = taskId,
            Type = "task.attachment-removed",
            Description = $"removed the attachment '{attachment.OriginalFileName}'."
        });
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _storage.DeleteAsync(attachment.StoredFileName, cancellationToken);

        return ProjectOperationResult<bool>.Success(true, "Project task attachment deleted");
    }

    private static string? ValidateFile(string originalFileName, string contentType, long sizeBytes)
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
            || !string.Equals(contentType?.Trim(), expectedContentType, StringComparison.OrdinalIgnoreCase))
        {
            return "Attachment format or content type is not allowed";
        }

        var normalizedName = Path.GetFileName(originalFileName.Trim());
        return normalizedName.Length > 255 ? "Attachment file name cannot exceed 255 characters" : null;
    }

    private async Task<ProjectMemberRole?> GetProjectRoleAsync(Guid userId, Guid projectId, CancellationToken cancellationToken)
    {
        var project = await _dbContext.Projects.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == projectId && !candidate.IsArchived, cancellationToken);
        if (project is null) return null;
        if (project.OwnerId == userId) return ProjectMemberRole.Owner;

        return await _dbContext.ProjectMembers
            .Where(member => member.ProjectId == projectId && member.UserId == userId && member.User.IsActive)
            .Select(member => (ProjectMemberRole?)member.Role)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<bool> HasProjectAccessAsync(Guid userId, Guid projectId, CancellationToken cancellationToken)
        => await GetProjectRoleAsync(userId, projectId, cancellationToken) is not null;

    private Task<bool> TaskBelongsToProjectAsync(Guid projectId, Guid taskId, CancellationToken cancellationToken)
        => _dbContext.ProjectTasks.AnyAsync(task => task.Id == taskId
            && task.ProjectId == projectId
            && _dbContext.Projects.Any(project => project.Id == projectId && !project.IsArchived), cancellationToken);
}