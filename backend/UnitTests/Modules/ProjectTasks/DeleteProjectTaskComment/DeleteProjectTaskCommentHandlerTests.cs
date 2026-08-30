using Application.Features.ProjectManagement.Tasks;
using Application.Features.Projects;
using Application.Modules.ProjectTasks.DeleteProjectTaskComment;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Modules.ProjectTasks.DeleteProjectTaskComment;
using Moq;

namespace UnitTests.Modules.ProjectTasks.DeleteProjectTaskComment;

public sealed class DeleteProjectTaskCommentHandlerTests
{
    private readonly Mock<IProjectTaskAccess> _access = new();
    private readonly Mock<IDeleteProjectTaskCommentStore> _commentStore = new();

    [Fact]
    public async Task Handle_returns_not_found_without_loading_comment_when_user_has_no_access()
    {
        var command = CreateCommand();
        _access
            .Setup(access => access.GetActiveProjectRoleAsync(
                command.UserId,
                command.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProjectMemberRole?)null);

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.NotFound, result.Status);
        _commentStore.Verify(
            store => store.GetAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_returns_not_found_when_comment_does_not_exist()
    {
        var command = CreateCommand();
        ConfigureAccess(command, ProjectMemberRole.Member);
        _commentStore
            .Setup(store => store.GetAsync(
                command.ProjectTaskId,
                command.CommentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProjectTaskComment?)null);

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.NotFound, result.Status);
        _commentStore.Verify(store => store.Remove(It.IsAny<ProjectTaskComment>()), Times.Never);
    }

    [Fact]
    public async Task Handle_forbids_member_from_deleting_another_users_comment()
    {
        var command = CreateCommand();
        ConfigureAccess(command, ProjectMemberRole.Member);
        var comment = CreateComment(Guid.NewGuid());
        _commentStore
            .Setup(store => store.GetAsync(
                command.ProjectTaskId,
                command.CommentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(comment);

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.Forbidden, result.Status);
        _commentStore.Verify(store => store.Remove(It.IsAny<ProjectTaskComment>()), Times.Never);
        _commentStore.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_allows_comment_author_to_delete_comment()
    {
        var command = CreateCommand();
        ConfigureAccess(command, ProjectMemberRole.Member);
        var comment = CreateComment(command.UserId);
        _commentStore
            .Setup(store => store.GetAsync(
                command.ProjectTaskId,
                command.CommentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(comment);
        _commentStore
            .Setup(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateHandler().HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        _commentStore.Verify(store => store.Remove(comment), Times.Once);
        _commentStore.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private DeleteProjectTaskCommentHandler CreateHandler()
        => new(_access.Object, _commentStore.Object);

    private void ConfigureAccess(
        DeleteProjectTaskCommentCommand command,
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
                command.ProjectTaskId,
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

    private static ProjectTaskComment CreateComment(Guid authorUserId)
        => new()
        {
            Id = Guid.NewGuid(),
            ProjectTaskId = Guid.NewGuid(),
            AuthorUserId = authorUserId,
            Content = "Comment"
        };

    private static DeleteProjectTaskCommentCommand CreateCommand()
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
}
