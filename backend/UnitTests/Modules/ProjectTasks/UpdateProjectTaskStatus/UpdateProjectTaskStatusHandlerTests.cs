using Application.Features.ProjectManagement.Tasks;
using Application.Features.Projects;
using Application.Modules.ProjectTasks.UpdateProjectTaskStatus;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Modules.ProjectTasks.UpdateProjectTaskStatus;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace UnitTests.Modules.ProjectTasks.UpdateProjectTaskStatus;

public sealed class UpdateProjectTaskStatusHandlerTests
{
    private readonly Mock<IProjectTaskAccess> _access = new();
    private readonly Mock<IProjectTaskCommandStore> _commandStore = new();

    [Fact]
    public async Task Handle_returns_not_found_without_loading_task_when_user_has_no_project_access()
    {
        var (command, _) = CreateScenario();
        _access
            .Setup(access => access.GetActiveProjectRoleAsync(
                command.UserId,
                command.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProjectMemberRole?)null);

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.NotFound, result.Status);
        _access.Verify(
            access => access.GetTaskWithLabelsAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_returns_forbidden_for_viewer_without_mutating()
    {
        var (command, task) = CreateScenario();
        ConfigureTaskAccess(command, task, ProjectMemberRole.Viewer);

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.Forbidden, result.Status);
        Assert.Equal(ProjectTaskStatus.Todo, task.Status);
        _commandStore.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_returns_forbidden_for_member_who_did_not_create_task()
    {
        var (command, task) = CreateScenario(Guid.NewGuid());
        ConfigureTaskAccess(command, task, ProjectMemberRole.Member);

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.Forbidden, result.Status);
        Assert.Equal(ProjectTaskStatus.Todo, task.Status);
        _commandStore.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_returns_validation_error_when_concurrency_stamp_is_missing()
    {
        var (scenarioCommand, task) = CreateScenario();
        var command = scenarioCommand with { ExpectedConcurrencyStamp = null };
        ConfigureTaskAccess(command, task, ProjectMemberRole.Owner);

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.ValidationError, result.Status);
        Assert.Equal("Project task concurrency stamp is required", result.Message);
        Assert.Equal(ProjectTaskStatus.Todo, task.Status);
        _commandStore.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_returns_conflict_for_a_stale_concurrency_stamp_without_mutating()
    {
        var (scenarioCommand, task) = CreateScenario();
        var command = scenarioCommand with { ExpectedConcurrencyStamp = "stale-stamp" };
        ConfigureTaskAccess(command, task, ProjectMemberRole.Owner);

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.Conflict, result.Status);
        Assert.Equal(ProjectTaskStatus.Todo, task.Status);
        _commandStore.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_updates_status_and_records_activity()
    {
        var (scenarioCommand, task) = CreateScenario();
        var command = scenarioCommand with { Status = ProjectTaskStatus.InProgress };
        ConfigureTaskAccess(command, task, ProjectMemberRole.Owner);

        var result = await CreateHandler().HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal("Project task status updated", result.Message);
        Assert.Equal(ProjectTaskStatus.InProgress, task.Status);
        Assert.Equal(ProjectTaskStatus.InProgress, result.Value?.Status);
        _commandStore.Verify(store => store.AddActivity(It.Is<ProjectActivity>(activity =>
            activity.ProjectId == command.ProjectId
            && activity.ProjectTaskId == command.TaskId
            && activity.ActorUserId == command.UserId
            && activity.Type == "task.status-changed"
            && activity.Description.Contains("InProgress", StringComparison.Ordinal))), Times.Once);
        _commandStore.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_saves_without_activity_when_status_does_not_change()
    {
        var (command, task) = CreateScenario();
        ConfigureTaskAccess(command, task, ProjectMemberRole.Owner);

        var result = await CreateHandler().HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal(ProjectTaskStatus.Todo, result.Value?.Status);
        _commandStore.Verify(store => store.AddActivity(It.IsAny<ProjectActivity>()), Times.Never);
        _commandStore.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_returns_conflict_when_persistence_detects_a_concurrent_change()
    {
        var (command, task) = CreateScenario();
        ConfigureTaskAccess(command, task, ProjectMemberRole.Owner);
        _commandStore
            .Setup(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException());

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.Conflict, result.Status);
        _commandStore.Verify(store => store.ClearChangeTracker(), Times.Once);
    }

    private UpdateProjectTaskStatusHandler CreateHandler()
        => new(_access.Object, _commandStore.Object);

    private void ConfigureTaskAccess(
        UpdateProjectTaskStatusCommand command,
        ProjectTask task,
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
            .ReturnsAsync(task);
    }

    private static (UpdateProjectTaskStatusCommand Command, ProjectTask Task) CreateScenario(Guid? createdByUserId = null)
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var task = ProjectTask.Create(
            projectId,
            "Task title",
            "Description",
            ProjectTaskPriority.Normal,
            null,
            null,
            createdByUserId ?? userId,
            ["zeta", "alpha"]);

        return (
            new(userId, projectId, task.Id, ProjectTaskStatus.Todo, task.ConcurrencyStamp),
            task);
    }
}
