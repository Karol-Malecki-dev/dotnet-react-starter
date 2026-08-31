using Application.Features.ProjectManagement.Tasks;
using Application.Features.Projects;
using Application.Modules.ProjectTasks.AssignmentNotifications;
using Application.Modules.ProjectTasks.CreateProjectTask;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Modules.ProjectTasks.CreateProjectTask;
using Moq;

namespace UnitTests.Modules.ProjectTasks.CreateProjectTask;

public sealed class CreateProjectTaskHandlerTests
{
    private readonly Mock<IProjectTaskAccess> _access = new();
    private readonly Mock<IProjectTaskCommandStore> _commandStore = new();
    private readonly Mock<IProjectTaskAssignmentNotificationWriter> _assignmentNotificationWriter = new();

    [Fact]
    public async Task Handle_returns_forbidden_for_viewer_without_persisting()
    {
        var command = CreateCommand();
        _access
            .Setup(access => access.GetActiveProjectRoleAsync(
                command.OwnerId,
                command.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProjectMemberRole.Viewer);

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.Forbidden, result.Status);
        _commandStore.Verify(store => store.AddTask(It.IsAny<ProjectTask>()), Times.Never);
        _commandStore.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_returns_validation_error_when_assignee_is_not_active_project_member()
    {
        var command = CreateCommand() with { AssignedUserId = Guid.NewGuid() };
        _access
            .Setup(access => access.GetActiveProjectRoleAsync(
                command.OwnerId,
                command.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProjectMemberRole.Owner);
        _commandStore
            .Setup(store => store.IsActiveProjectMemberAsync(
                command.ProjectId,
                command.AssignedUserId!.Value,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.ValidationError, result.Status);
        Assert.Equal("Assigned user is not an active member of this project", result.Message);
        _commandStore.Verify(store => store.AddTask(It.IsAny<ProjectTask>()), Times.Never);
    }

    [Fact]
    public async Task Handle_persists_task_and_returns_created_result_for_owner()
    {
        var command = CreateCommand();
        _access
            .Setup(access => access.GetActiveProjectRoleAsync(
                command.OwnerId,
                command.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProjectMemberRole.Owner);

        var result = await CreateHandler().HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal(201, result.CreatedStatusCode);
        Assert.Equal("Project task created", result.Message);
        Assert.NotNull(result.Value);
        Assert.Equal(command.Title, result.Value!.Title);
        _commandStore.Verify(
            store => store.AddTask(It.Is<ProjectTask>(task => task.ProjectId == command.ProjectId)),
            Times.Once);
        _commandStore.Verify(store => store.AddActivity(It.IsAny<ProjectActivity>()), Times.Once);
        _commandStore.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_prepares_notification_for_a_different_assignee_before_persisting_task()
    {
        var assignedUserId = Guid.NewGuid();
        var command = CreateCommand() with { AssignedUserId = assignedUserId };
        _access
            .Setup(access => access.GetActiveProjectRoleAsync(
                command.OwnerId,
                command.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProjectMemberRole.Owner);
        _commandStore
            .Setup(store => store.IsActiveProjectMemberAsync(
                command.ProjectId,
                assignedUserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateHandler().HandleAsync(command);

        Assert.True(result.IsSuccess);
        _assignmentNotificationWriter.Verify(writer => writer.AddTaskAssignedNotificationAsync(
            assignedUserId,
            command.ProjectId,
            It.IsAny<Guid>(),
            command.Title,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_does_not_persist_when_assignment_notification_preparation_fails()
    {
        var assignedUserId = Guid.NewGuid();
        var command = CreateCommand() with { AssignedUserId = assignedUserId };
        _access
            .Setup(access => access.GetActiveProjectRoleAsync(
                command.OwnerId,
                command.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProjectMemberRole.Owner);
        _commandStore
            .Setup(store => store.IsActiveProjectMemberAsync(
                command.ProjectId,
                assignedUserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _assignmentNotificationWriter
            .Setup(writer => writer.AddTaskAssignedNotificationAsync(
                assignedUserId,
                command.ProjectId,
                It.IsAny<Guid>(),
                command.Title,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("notification preparation failed"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateHandler().HandleAsync(command));

        _commandStore.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private CreateProjectTaskHandler CreateHandler()
        => new(_access.Object, _commandStore.Object, _assignmentNotificationWriter.Object);

    private static CreateProjectTaskCommand CreateCommand()
        => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Task title",
            "Description",
            ProjectTaskPriority.Normal,
            null,
            null,
            []);
}
