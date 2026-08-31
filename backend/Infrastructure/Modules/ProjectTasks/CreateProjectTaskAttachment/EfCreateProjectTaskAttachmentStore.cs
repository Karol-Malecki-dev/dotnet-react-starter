using Application.Modules.ProjectTasks.Attachments;
using Application.Modules.ProjectTasks.CreateProjectTaskAttachment;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Settings;

namespace Infrastructure.Modules.ProjectTasks.CreateProjectTaskAttachment;

/// <summary>
/// EF Core implementation of the create-attachment persistence port.
/// </summary>
public sealed class EfCreateProjectTaskAttachmentStore : ICreateProjectTaskAttachmentStore
{
    private readonly ApplicationDbContext _dbContext;
    private readonly AttachmentSettings _settings;

    public EfCreateProjectTaskAttachmentStore(ApplicationDbContext dbContext, IOptions<AttachmentSettings> settings)
    {
        _dbContext = dbContext;
        _settings = settings.Value;
    }

    public async Task<ProjectTaskAttachmentView> CreateAsync(
        CreateProjectTaskAttachmentCommand command,
        string storedFileName,
        CancellationToken cancellationToken = default)
    {
        var isPostgreSql = string.Equals(
            _dbContext.Database.ProviderName,
            "Npgsql.EntityFrameworkCore.PostgreSQL",
            StringComparison.Ordinal);
        await using var transaction = isPostgreSql
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        if (isPostgreSql)
        {
            await _dbContext.ProjectTasks
                .FromSqlInterpolated($"SELECT * FROM \"ProjectTasks\" WHERE \"Id\" = {command.TaskId} FOR UPDATE")
                .Select(task => task.Id)
                .SingleAsync(cancellationToken);
        }

        var attachmentCount = await _dbContext.ProjectTaskAttachments
            .CountAsync(attachment => attachment.ProjectTaskId == command.TaskId, cancellationToken);
        var attachmentBytes = await _dbContext.ProjectTaskAttachments
            .Where(attachment => attachment.ProjectTaskId == command.TaskId)
            .SumAsync(attachment => (long?)attachment.SizeBytes, cancellationToken) ?? 0;

        if (attachmentCount >= _settings.MaxCountPerTask)
        {
            throw new ProjectTaskAttachmentQuotaExceededException(
                $"A task cannot contain more than {_settings.MaxCountPerTask} attachments.");
        }

        if (attachmentBytes > _settings.MaxBytesPerTask - command.SizeBytes)
        {
            throw new ProjectTaskAttachmentQuotaExceededException(
                "The task attachment size quota would be exceeded.");
        }

        var attachment = new ProjectTaskAttachment
        {
            ProjectTaskId = command.TaskId,
            UploadedByUserId = command.UserId,
            OriginalFileName = command.OriginalFileName,
            StoredFileName = storedFileName,
            ContentType = command.ContentType,
            SizeBytes = command.SizeBytes
        };
        _dbContext.ProjectTaskAttachments.Add(attachment);
        _dbContext.ProjectActivities.Add(new ProjectActivity
        {
            ProjectId = command.ProjectId,
            ActorUserId = command.UserId,
            ProjectTaskId = command.TaskId,
            Type = "task.attachment-added",
            Description = $"added the attachment '{command.OriginalFileName}'."
        });
        await _dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        var uploaderDisplayName = await _dbContext.Users
            .Where(user => user.Id == command.UserId)
            .Select(user => user.DisplayName)
            .SingleAsync(cancellationToken);

        return new ProjectTaskAttachmentView(
            attachment.Id,
            attachment.ProjectTaskId,
            attachment.UploadedByUserId,
            uploaderDisplayName.Value,
            attachment.OriginalFileName,
            attachment.ContentType,
            attachment.SizeBytes,
            attachment.CreatedAt);
    }
}
