using Application.Features.ProjectManagement.Tasks;
using Application.Features.Projects;
using Application.Interfaces;
using Application.Modules.ProjectTasks.CreateProjectTask;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Modules.ProjectTasks.CreateProjectTask;
using Moq;

namespace UnitTests.Features.ProjectManagement.Tasks;

public sealed class CreateProjectTaskHandlerTests
{
    private readonly Mock<IProjectTaskAccess> _access = new();
    private readonly Mock<IProjectTaskCommandStore> _commandStore = new();
    private readonly Mock<INotificationService> _notificationService = new();

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
    public async Task Handle_notifies_different_assignee_after_persisting_task()
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
        _notificationService.Verify(notification => notification.CreateAsync(
            assignedUserId,
            NotificationType.TaskAssigned,
            "You were assigned a task",
            It.Is<string>(message => message.Contains(command.Title, StringComparison.Ordinal)),
            "ProjectTask",
            It.IsAny<Guid>(),
            command.ProjectId,
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private CreateProjectTaskHandler CreateHandler()
        => new(_access.Object, _commandStore.Object, _notificationService.Object);

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
