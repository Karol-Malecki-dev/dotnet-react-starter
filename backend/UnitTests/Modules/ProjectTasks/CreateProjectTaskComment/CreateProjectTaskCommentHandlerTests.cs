using Application.Features.ProjectManagement.Tasks;
using Application.Features.Projects;
using Application.Modules.ProjectTasks.Comments;
using Application.Modules.ProjectTasks.CreateProjectTaskComment;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Modules.ProjectTasks.CreateProjectTaskComment;
using Moq;

namespace UnitTests.Modules.ProjectTasks.CreateProjectTaskComment;

public sealed class CreateProjectTaskCommentHandlerTests
{
    private readonly Mock<IProjectTaskAccess> _access = new();
    private readonly Mock<ICreateProjectTaskCommentStore> _commentStore = new();

    [Fact]
    public async Task Handle_rejects_empty_content_before_access_checks()
    {
        var command = CreateCommand("  ");

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.ValidationError, result.Status);
        _access.Verify(
            access => access.GetActiveProjectRoleAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_returns_not_found_when_user_has_no_project_access()
    {
        var command = CreateCommand("A comment");
        _access
            .Setup(access => access.GetActiveProjectRoleAsync(
                command.AuthorUserId,
                command.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProjectMemberRole?)null);

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.NotFound, result.Status);
        _commentStore.Verify(
            store => store.CreateAsync(It.IsAny<CreateProjectTaskCommentCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_forbids_viewer_members()
    {
        var command = CreateCommand("A comment");
        ConfigureAccess(command, ProjectMemberRole.Viewer);

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.Forbidden, result.Status);
        _commentStore.Verify(
            store => store.CreateAsync(It.IsAny<CreateProjectTaskCommentCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_trims_content_and_returns_created_comment()
    {
        var command = CreateCommand("  A comment  ");
        ConfigureAccess(command, ProjectMemberRole.Member);
        var expected = new ProjectTaskCommentView(
            Guid.NewGuid(),
            command.ProjectTaskId,
            command.AuthorUserId,
            "Member",
            "A comment",
            DateTime.UtcNow);
        _commentStore
            .Setup(store => store.CreateAsync(
                It.Is<CreateProjectTaskCommentCommand>(candidate => candidate.Content == "A comment"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await CreateHandler().HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal(201, result.CreatedStatusCode);
        Assert.Same(expected, result.Value);
        _commentStore.Verify(
            store => store.CreateAsync(
                It.Is<CreateProjectTaskCommentCommand>(candidate => candidate.Content == "A comment"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private CreateProjectTaskCommentHandler CreateHandler()
        => new(_access.Object, _commentStore.Object);

    private void ConfigureAccess(
        CreateProjectTaskCommentCommand command,
        ProjectMemberRole role)
    {
        _access
            .Setup(access => access.GetActiveProjectRoleAsync(
                command.AuthorUserId,
                command.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);
        _access
            .Setup(access => access.GetTaskWithLabelsAsync(
                command.ProjectId,
                command.ProjectTaskId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProjectTask.Create(
                command.ProjectId,
                "Task",
                null,
                ProjectTaskPriority.Normal,
                null,
                null,
                command.AuthorUserId));
    }

    private static CreateProjectTaskCommentCommand CreateCommand(string content)
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), content);
}
