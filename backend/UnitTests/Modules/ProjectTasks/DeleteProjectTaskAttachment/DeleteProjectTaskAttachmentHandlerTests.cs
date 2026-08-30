using Application.Features.ProjectManagement.Tasks;
using Application.Features.Projects;
using Application.Modules.ProjectTasks.Attachments;
using Application.Modules.ProjectTasks.DeleteProjectTaskAttachment;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Modules.ProjectTasks.DeleteProjectTaskAttachment;
using Moq;

namespace UnitTests.Modules.ProjectTasks.DeleteProjectTaskAttachment;

public sealed class DeleteProjectTaskAttachmentHandlerTests
{
    private readonly Mock<IProjectTaskAccess> _access = new();
    private readonly Mock<IDeleteProjectTaskAttachmentStore> _attachmentStore = new();
    private readonly Mock<IProjectTaskAttachmentStorage> _storage = new();

    [Fact]
    public async Task Handle_forbids_member_from_deleting_another_users_attachment()
    {
        var command = CreateCommand();
        ConfigureAccess(command, ProjectMemberRole.Member);
        var attachment = CreateAttachment(command, Guid.NewGuid());
        _attachmentStore
            .Setup(store => store.GetAsync(
                command.TaskId,
                command.AttachmentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(attachment);

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.Forbidden, result.Status);
        _attachmentStore.Verify(
            store => store.DeleteAsync(
                It.IsAny<ProjectTaskAttachment>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
        _storage.Verify(
            storage => storage.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_deletes_metadata_then_binary_for_the_uploader()
    {
        var command = CreateCommand();
        ConfigureAccess(command, ProjectMemberRole.Member);
        var attachment = CreateAttachment(command, command.UserId);
        _attachmentStore
            .Setup(store => store.GetAsync(
                command.TaskId,
                command.AttachmentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(attachment);
        _attachmentStore
            .Setup(store => store.DeleteAsync(
                attachment,
                command.ProjectId,
                command.UserId,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _storage
            .Setup(storage => storage.DeleteAsync(
                attachment.StoredFileName,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateHandler().HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        _attachmentStore.Verify(
            store => store.DeleteAsync(
                attachment,
                command.ProjectId,
                command.UserId,
                It.IsAny<CancellationToken>()),
            Times.Once);
        _storage.Verify(
            storage => storage.DeleteAsync(
                attachment.StoredFileName,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private DeleteProjectTaskAttachmentHandler CreateHandler()
        => new(_access.Object, _attachmentStore.Object, _storage.Object);

    private void ConfigureAccess(
        DeleteProjectTaskAttachmentCommand command,
        ProjectMemberRole role)
    {
        _access
            .Setup(access => access.GetActiveProjectRoleAsync(
                command.UserId,
                command.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);
        _access
            .Setup(access => access.GetTaskWithLabelsAsync(
                command.ProjectId,
                command.TaskId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProjectTask.Create(
                command.ProjectId,
                "Task",
                null,
                ProjectTaskPriority.Normal,
                null,
                null,
                command.UserId));
    }

    private static ProjectTaskAttachment CreateAttachment(
        DeleteProjectTaskAttachmentCommand command,
        Guid uploaderId)
        => new()
        {
            Id = command.AttachmentId,
            ProjectTaskId = command.TaskId,
            UploadedByUserId = uploaderId,
            OriginalFileName = "notes.txt",
            StoredFileName = "stored.txt",
            ContentType = "text/plain",
            SizeBytes = 10
        };

    private static DeleteProjectTaskAttachmentCommand CreateCommand()
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
}
