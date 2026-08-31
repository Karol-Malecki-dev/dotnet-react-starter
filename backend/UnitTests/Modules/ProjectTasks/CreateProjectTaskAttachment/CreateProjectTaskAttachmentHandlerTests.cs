using Application.Features.ProjectManagement.Tasks;
using Application.Features.Projects;
using Application.Modules.ProjectTasks.Attachments;
using Application.Modules.ProjectTasks.CreateProjectTaskAttachment;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Modules.ProjectTasks.CreateProjectTaskAttachment;
using Microsoft.Extensions.Options;
using Shared.Settings;
using Moq;
using System.IO.Compression;

namespace UnitTests.Modules.ProjectTasks.CreateProjectTaskAttachment;

public sealed class CreateProjectTaskAttachmentHandlerTests
{
    private readonly Mock<IProjectTaskAccess> _access = new();
    private readonly Mock<ICreateProjectTaskAttachmentStore> _attachmentStore = new();
    private readonly Mock<IProjectTaskAttachmentStorage> _storage = new();
    private readonly Mock<IProjectTaskAttachmentMalwareScanner> _malwareScanner = new();

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
    public async Task Handle_rejects_file_when_content_does_not_match_declared_pdf_format()
    {
        var content = new MemoryStream("MZ executable payload"u8.ToArray());
        var command = CreateCommand() with
        {
            OriginalFileName = "report.pdf",
            ContentType = "application/pdf",
            SizeBytes = content.Length,
            Content = content
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
    public async Task Handle_rejects_file_when_declared_size_does_not_match_content()
    {
        var command = CreateCommand() with { SizeBytes = 1 };
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
    public async Task Handle_rejects_binary_content_declared_as_text()
    {
        var content = new MemoryStream([0x00, 0x01, 0x02, 0x03]);
        var command = CreateCommand() with
        {
            SizeBytes = content.Length,
            Content = content
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

    [Theory]
    [MemberData(nameof(AllowedFileContents))]
    public async Task Handle_accepts_content_matching_declared_format(
        string fileName,
        string contentType,
        byte[] bytes)
    {
        var content = new MemoryStream(bytes);
        var command = CreateCommand() with
        {
            OriginalFileName = fileName,
            ContentType = contentType,
            SizeBytes = content.Length,
            Content = content
        };
        ConfigureAccess(command, ProjectMemberRole.Owner);
        var expected = new ProjectTaskAttachmentView(
            Guid.NewGuid(),
            command.TaskId,
            command.UserId,
            "Owner",
            fileName,
            contentType,
            command.SizeBytes,
            DateTime.UtcNow);
        _storage
            .Setup(storage => storage.SaveAsync(
                content,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => Assert.Equal(0, content.Position))
            .Returns(Task.CompletedTask);
        _attachmentStore
            .Setup(store => store.CreateAsync(
                It.IsAny<CreateProjectTaskAttachmentCommand>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await CreateHandler().HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Same(expected, result.Value);
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

    [Fact]
    public async Task Handle_removes_binary_when_attachment_quota_is_exceeded()
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
            .ThrowsAsync(new ProjectTaskAttachmentQuotaExceededException("Attachment quota exceeded."));
        _storage
            .Setup(storage => storage.DeleteAsync(
                It.IsAny<string>(),
                CancellationToken.None))
            .Returns(Task.CompletedTask);

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.Conflict, result.Status);
        Assert.Equal("Attachment quota exceeded.", result.Message);
        _storage.Verify(
            storage => storage.DeleteAsync(
                It.Is<string>(fileName => fileName.EndsWith(".txt", StringComparison.Ordinal)),
                CancellationToken.None),
            Times.Once);
    }

    [Theory]
    [InlineData(ProjectTaskAttachmentScanStatus.ThreatDetected, ProjectOperationStatus.ValidationError)]
    [InlineData(ProjectTaskAttachmentScanStatus.Unavailable, ProjectOperationStatus.Conflict)]
    public async Task Handle_rejects_non_clean_scan_before_storing_file(
        ProjectTaskAttachmentScanStatus scanStatus,
        ProjectOperationStatus expectedStatus)
    {
        var command = CreateCommand();
        ConfigureAccess(command, ProjectMemberRole.Owner);
        _malwareScanner
            .Setup(scanner => scanner.ScanAsync(
                command.Content,
                command.OriginalFileName,
                command.ContentType,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(scanStatus);

        var result = await CreateHandler(requireMalwareScan: true).HandleAsync(command);

        Assert.Equal(expectedStatus, result.Status);
        _storage.Verify(
            storage => storage.SaveAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_stores_clean_scanned_file_from_the_start_of_the_stream()
    {
        var command = CreateCommand();
        ConfigureAccess(command, ProjectMemberRole.Owner);
        _malwareScanner
            .Setup(scanner => scanner.ScanAsync(
                command.Content,
                command.OriginalFileName,
                command.ContentType,
                It.IsAny<CancellationToken>()))
            .Callback(() => command.Content.Position = command.Content.Length)
            .ReturnsAsync(ProjectTaskAttachmentScanStatus.Clean);
        _storage
            .Setup(storage => storage.SaveAsync(
                command.Content,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => Assert.Equal(0, command.Content.Position))
            .Returns(Task.CompletedTask);
        _attachmentStore
            .Setup(store => store.CreateAsync(
                It.IsAny<CreateProjectTaskAttachmentCommand>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProjectTaskAttachmentView(
                Guid.NewGuid(),
                command.TaskId,
                command.UserId,
                "Owner",
                command.OriginalFileName,
                command.ContentType,
                command.SizeBytes,
                DateTime.UtcNow));

        var result = await CreateHandler(requireMalwareScan: true).HandleAsync(command);

        Assert.True(result.IsSuccess);
    }

    private CreateProjectTaskAttachmentHandler CreateHandler(bool requireMalwareScan = false)
        => new(
            _access.Object,
            _attachmentStore.Object,
            _storage.Object,
            _malwareScanner.Object,
            Options.Create(new AttachmentSettings { RequireMalwareScan = requireMalwareScan }));

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
    {
        var content = new MemoryStream("test file\n"u8.ToArray());
        return new CreateProjectTaskAttachmentCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "notes.txt",
            "text/plain",
            content.Length,
            content);
    }

    public static TheoryData<string, string, byte[]> AllowedFileContents()
        => new()
        {
            { "document.pdf", "application/pdf", "%PDF-1.7\n"u8.ToArray() },
            { "image.png", "image/png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A] },
            { "image.jpg", "image/jpeg", [0xFF, 0xD8, 0xFF, 0xE0] },
            { "image.jpeg", "image/jpeg", [0xFF, 0xD8, 0xFF, 0xE1] },
            { "document.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", CreateOpenXmlPackage("word/document.xml") },
            { "workbook.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", CreateOpenXmlPackage("xl/workbook.xml") },
            { "notes.txt", "text/plain", "plain text\n"u8.ToArray() }
        };

    private static byte[] CreateOpenXmlPackage(string documentEntryName)
    {
        using var content = new MemoryStream();
        using (var archive = new ZipArchive(content, ZipArchiveMode.Create, leaveOpen: true))
        {
            archive.CreateEntry("[Content_Types].xml");
            archive.CreateEntry("_rels/.rels");
            archive.CreateEntry(documentEntryName);
        }

        return content.ToArray();
    }
}
