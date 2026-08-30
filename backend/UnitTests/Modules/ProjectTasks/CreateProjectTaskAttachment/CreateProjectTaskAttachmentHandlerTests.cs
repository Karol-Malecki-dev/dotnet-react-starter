using Application.Features.ProjectManagement.Tasks;
using Application.Features.Projects;
using Application.Modules.ProjectTasks.Attachments;
using Application.Modules.ProjectTasks.CreateProjectTaskAttachment;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Modules.ProjectTasks.CreateProjectTaskAttachment;
using Moq;

namespace UnitTests.Modules.ProjectTasks.CreateProjectTaskAttachment;

public sealed class CreateProjectTaskAttachmentHandlerTests
{
    private readonly Mock<IProjectTaskAccess> _access = new();
    private readonly Mock<ICreateProjectTaskAttachmentStore> _attachmentStore = new();
    private readonly Mock<IProjectTaskAttachmentStorage> _storage = new();

    [Fact]
    public async Task Handle_forbids_viewer_before_storing_file()
    {
        var command = CreateCommand();
        ConfigureAccess(command, ProjectMemberRole.Viewer);

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.Forbidden, result.Status);
        _storage.Verify(
            storage => storage.SaveAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_rejects_invalid_file_without_storing_file()
    {
        var command = CreateCommand() with
        {
            OriginalFileName = "notes.exe",
            ContentType = "application/octet-stream"
        };
        ConfigureAccess(command, ProjectMemberRole.Owner);

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.ValidationError, result.Status);
        _storage.Verify(
            storage => storage.SaveAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_stores_file_and_returns_created_metadata()
    {
        var command = CreateCommand() with
        {
            OriginalFileName = "folder\\notes.txt",
            ContentType = " text/plain "
        };
        ConfigureAccess(command, ProjectMemberRole.Owner);
        var expected = new ProjectTaskAttachmentView(
            Guid.NewGuid(),
            command.TaskId,
            command.UserId,
            "Owner",
            "notes.txt",
            "text/plain",
            command.SizeBytes,
            DateTime.UtcNow);
        _storage
            .Setup(storage => storage.SaveAsync(
                command.Content,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _attachmentStore
            .Setup(store => store.CreateAsync(
                It.Is<CreateProjectTaskAttachmentCommand>(candidate =>
                    candidate.OriginalFileName == "notes.txt"
                    && candidate.ContentType == "text/plain"),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await CreateHandler().HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal(201, result.CreatedStatusCode);
        Assert.Same(expected, result.Value);
        _storage.Verify(
            storage => storage.SaveAsync(
                command.Content,
                It.Is<string>(fileName => fileName.EndsWith(".txt", StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_removes_binary_when_metadata_persistence_fails()
    {
        var command = CreateCommand();
        ConfigureAccess(command, ProjectMemberRole.Owner);
        _storage
            .Setup(storage => storage.SaveAsync(
                command.Content,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _attachmentStore
            .Setup(store => store.CreateAsync(
                It.IsAny<CreateProjectTaskAttachmentCommand>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database failure"));
        _storage
            .Setup(storage => storage.DeleteAsync(
                It.IsAny<string>(),
                CancellationToken.None))
            .Returns(Task.CompletedTask);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateHandler().HandleAsync(command));

        _storage.Verify(
            storage => storage.DeleteAsync(
                It.Is<string>(fileName => fileName.EndsWith(".txt", StringComparison.Ordinal)),
                CancellationToken.None),
            Times.Once);
    }

    private CreateProjectTaskAttachmentHandler CreateHandler()
        => new(_access.Object, _attachmentStore.Object, _storage.Object);

    private void ConfigureAccess(
        CreateProjectTaskAttachmentCommand command,
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

    private static CreateProjectTaskAttachmentCommand CreateCommand()
        => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "notes.txt",
            "text/plain",
            10,
            new MemoryStream(new byte[10]));
}
