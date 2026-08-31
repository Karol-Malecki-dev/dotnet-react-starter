using Application.Features.Projects;
using Application.Modules.Projects.RemoveProjectMember;
using Application.Modules.ProjectTasks.Assignments;
using Domain.Entities;
using Infrastructure.Modules.Projects.RemoveProjectMember;
using Moq;

namespace UnitTests.Modules.Projects.RemoveProjectMember;

public sealed class RemoveProjectMemberHandlerTests
{
    private readonly Mock<IRemoveProjectMemberStore> _store = new();
    private readonly Mock<IProjectTaskMemberAssignmentWriter> _taskAssignmentWriter = new();

    [Fact]
    public async Task Handle_returns_not_found_when_project_is_not_owned_by_user()
    {
        var command = CreateCommand();
        _store
            .Setup(store => store.GetOwnedProjectWithMembersAsync(
                command.OwnerId,
                command.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project?)null);

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.NotFound, result.Status);
        _taskAssignmentWriter.Verify(writer => writer.UnassignAllAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _store.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_rejects_removing_project_owner()
    {
        var ownerId = Guid.NewGuid();
        var project = Project.Create(ownerId, "Project");
        var command = new RemoveProjectMemberCommand(ownerId, project.Id, ownerId);
        SetupProject(command, project);

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.Conflict, result.Status);
        _taskAssignmentWriter.Verify(writer => writer.UnassignAllAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _store.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_returns_not_found_when_member_does_not_exist()
    {
        var ownerId = Guid.NewGuid();
        var project = Project.Create(ownerId, "Project");
        var command = new RemoveProjectMemberCommand(ownerId, project.Id, Guid.NewGuid());
        SetupProject(command, project);

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.NotFound, result.Status);
        _taskAssignmentWriter.Verify(writer => writer.UnassignAllAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _store.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_unassigns_tasks_and_removes_member_in_one_save()
    {
        var ownerId = Guid.NewGuid();
        var project = Project.Create(ownerId, "Project");
        var memberId = Guid.NewGuid();
        var member = project.AddMember(memberId);
        var command = new RemoveProjectMemberCommand(ownerId, project.Id, memberId);
        SetupProject(command, project);
        _taskAssignmentWriter
            .Setup(writer => writer.UnassignAllAsync(
                command.ProjectId,
                command.UserId,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _store
            .Setup(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateHandler().HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        Assert.DoesNotContain(project.Members, candidate => candidate.UserId == memberId);
        _store.Verify(store => store.RemoveMember(member), Times.Once);
        _store.Verify(store => store.AddActivity(It.Is<ProjectActivity>(activity =>
            activity.ProjectId == command.ProjectId
            && activity.ActorUserId == command.OwnerId
            && activity.Type == "member.removed")), Times.Once);
        _store.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_does_not_remove_member_when_task_unassignment_fails()
    {
        var ownerId = Guid.NewGuid();
        var project = Project.Create(ownerId, "Project");
        var memberId = Guid.NewGuid();
        project.AddMember(memberId);
        var command = new RemoveProjectMemberCommand(ownerId, project.Id, memberId);
        SetupProject(command, project);
        _taskAssignmentWriter
            .Setup(writer => writer.UnassignAllAsync(
                command.ProjectId,
                command.UserId,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Task assignment update failed."));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateHandler().HandleAsync(command));

        Assert.Contains(project.Members, candidate => candidate.UserId == memberId);
        _store.Verify(store => store.RemoveMember(It.IsAny<ProjectMember>()), Times.Never);
        _store.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_forwards_cancellation_token_to_all_operations()
    {
        var ownerId = Guid.NewGuid();
        var project = Project.Create(ownerId, "Project");
        var memberId = Guid.NewGuid();
        project.AddMember(memberId);
        var command = new RemoveProjectMemberCommand(ownerId, project.Id, memberId);
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        _store
            .Setup(store => store.GetOwnedProjectWithMembersAsync(
                ownerId,
                project.Id,
                cancellationToken))
            .ReturnsAsync(project);
        _taskAssignmentWriter
            .Setup(writer => writer.UnassignAllAsync(project.Id, memberId, cancellationToken))
            .Returns(Task.CompletedTask);
        _store
            .Setup(store => store.SaveChangesAsync(cancellationToken))
            .Returns(Task.CompletedTask);

        await CreateHandler().HandleAsync(command, cancellationToken);

        _store.Verify(store => store.GetOwnedProjectWithMembersAsync(
            ownerId,
            project.Id,
            cancellationToken), Times.Once);
        _taskAssignmentWriter.Verify(writer => writer.UnassignAllAsync(
            project.Id,
            memberId,
            cancellationToken), Times.Once);
        _store.Verify(store => store.SaveChangesAsync(cancellationToken), Times.Once);
    }

    private void SetupProject(RemoveProjectMemberCommand command, Project project)
        => _store
            .Setup(store => store.GetOwnedProjectWithMembersAsync(
                command.OwnerId,
                command.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

    private RemoveProjectMemberHandler CreateHandler()
        => new(_store.Object, _taskAssignmentWriter.Object);

    private static RemoveProjectMemberCommand CreateCommand()
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
}
