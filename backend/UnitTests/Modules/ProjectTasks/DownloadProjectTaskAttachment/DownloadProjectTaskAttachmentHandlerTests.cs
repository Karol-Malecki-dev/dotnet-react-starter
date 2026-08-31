using Application.Features.ProjectManagement.Tasks;
using Application.Features.Projects;
using Application.Modules.ProjectTasks.Attachments;
using Application.Modules.ProjectTasks.DownloadProjectTaskAttachment;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Modules.ProjectTasks.DownloadProjectTaskAttachment;
using Moq;

namespace UnitTests.Modules.ProjectTasks.DownloadProjectTaskAttachment;

public sealed class DownloadProjectTaskAttachmentHandlerTests
{
    private readonly Mock<IProjectTaskAccess> _access = new();
    private readonly Mock<IDownloadProjectTaskAttachmentStore> _attachmentStore = new();
    private readonly Mock<IProjectTaskAttachmentStorage> _storage = new();

    [Fact]
    public async Task Handle_returns_not_found_without_loading_attachment_when_user_has_no_access()
    {
        var query = CreateQuery();
        _access
            .Setup(access => access.GetActiveProjectRoleAsync(
                query.UserId,
                query.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProjectMemberRole?)null);

        var result = await CreateHandler().HandleAsync(query);

        Assert.Equal(ProjectOperationStatus.NotFound, result.Status);
        _attachmentStore.Verify(
            store => store.GetAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_returns_not_found_when_binary_file_is_missing()
    {
        var query = CreateQuery();
        ConfigureAccess(query);
        var attachment = CreateAttachment(query);
        _attachmentStore
            .Setup(store => store.GetAsync(
                query.TaskId,
                query.AttachmentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(attachment);
        _storage
            .Setup(storage => storage.OpenReadAsync(
                attachment.StoredFileName,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream?)null);

        var result = await CreateHandler().HandleAsync(query);

        Assert.Equal(ProjectOperationStatus.NotFound, result.Status);
        Assert.Equal("Project task attachment file not found", result.Message);
    }

    [Fact]
    public async Task Handle_returns_binary_stream_and_metadata()
    {
        var query = CreateQuery();
        ConfigureAccess(query);
        var attachment = CreateAttachment(query);
        var stream = new MemoryStream([1, 2, 3]);
        _attachmentStore
            .Setup(store => store.GetAsync(
                query.TaskId,
                query.AttachmentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(attachment);
        _storage
            .Setup(storage => storage.OpenReadAsync(
                attachment.StoredFileName,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(stream);

        var result = await CreateHandler().HandleAsync(query);

        Assert.True(result.IsSuccess);
        Assert.Same(stream, result.Value!.Content);
        Assert.Equal(attachment.OriginalFileName, result.Value.OriginalFileName);
        Assert.Equal(attachment.ContentType, result.Value.ContentType);
    }

    private DownloadProjectTaskAttachmentHandler CreateHandler()
        => new(_access.Object, _attachmentStore.Object, _storage.Object);

    private void ConfigureAccess(DownloadProjectTaskAttachmentQuery query)
    {
        _access
            .Setup(access => access.GetActiveProjectRoleAsync(
                query.UserId,
                query.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProjectMemberRole.Member);
        _access
            .Setup(access => access.GetTaskWithLabelsAsync(
                query.ProjectId,
                query.TaskId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProjectTask.Create(
                query.ProjectId,
                "Task",
                null,
                ProjectTaskPriority.Normal,
                null,
                null,
                query.UserId));
    }

    private static ProjectTaskAttachment CreateAttachment(
        DownloadProjectTaskAttachmentQuery query)
        => new()
        {
            Id = query.AttachmentId,
            ProjectTaskId = query.TaskId,
            UploadedByUserId = query.UserId,
            OriginalFileName = "notes.txt",
            StoredFileName = "stored.txt",
            ContentType = "text/plain",
            SizeBytes = 3
        };

    private static DownloadProjectTaskAttachmentQuery CreateQuery()
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
}
