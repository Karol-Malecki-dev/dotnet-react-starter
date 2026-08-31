using Application.Features.ProjectManagement.Tasks;
using Application.Features.Projects;
using Application.Modules.ProjectTasks.Attachments;
using Application.Modules.ProjectTasks.DeleteProjectTask;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Modules.ProjectTasks.DeleteProjectTask;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace UnitTests.Modules.ProjectTasks.DeleteProjectTask;

public sealed class DeleteProjectTaskHandlerTests
{
    private readonly Mock<IProjectTaskAccess> _access = new();
    private readonly Mock<IProjectTaskCommandStore> _commandStore = new();
    private readonly Mock<IProjectTaskAttachmentCleanupQueue> _cleanupQueue = new();

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
    public async Task Handle_returns_forbidden_for_viewer_without_removing_task()
    {
        var (command, task) = CreateScenario();
        ConfigureTaskAccess(command, task, ProjectMemberRole.Viewer);

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.Forbidden, result.Status);
        _commandStore.Verify(store => store.RemoveTask(It.IsAny<ProjectTask>()), Times.Never);
        _commandStore.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_returns_forbidden_for_member_who_did_not_create_task()
    {
        var (command, task) = CreateScenario(Guid.NewGuid());
        ConfigureTaskAccess(command, task, ProjectMemberRole.Member);

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.Forbidden, result.Status);
        _commandStore.Verify(store => store.RemoveTask(It.IsAny<ProjectTask>()), Times.Never);
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
        _commandStore.Verify(store => store.RemoveTask(It.IsAny<ProjectTask>()), Times.Never);
    }

    [Fact]
    public async Task Handle_returns_conflict_for_a_stale_concurrency_stamp_without_removing_task()
    {
        var (scenarioCommand, task) = CreateScenario();
        var command = scenarioCommand with { ExpectedConcurrencyStamp = "stale-stamp" };
        ConfigureTaskAccess(command, task, ProjectMemberRole.Owner);

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.Conflict, result.Status);
        _commandStore.Verify(store => store.RemoveTask(It.IsAny<ProjectTask>()), Times.Never);
        _commandStore.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_removes_task_when_concurrency_stamp_matches()
    {
        var (command, task) = CreateScenario();
        ConfigureTaskAccess(command, task, ProjectMemberRole.Owner);

        var result = await CreateHandler().HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        Assert.Equal("Project task deleted", result.Message);
        _commandStore.Verify(store => store.RemoveTask(task), Times.Once);
        _commandStore.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_queues_each_attachment_file_once_before_removing_task()
    {
        var (command, task) = CreateScenario();
        ConfigureTaskAccess(command, task, ProjectMemberRole.Owner);
        var handler = CreateHandler();
        _cleanupQueue
            .Setup(queue => queue.PrepareTaskDeletionAsync(
                task.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(["first.bin", "second.bin", "first.bin"]);

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsSuccess);
        _cleanupQueue.Verify(queue => queue.Enqueue("first.bin"), Times.Once);
        _cleanupQueue.Verify(queue => queue.Enqueue("second.bin"), Times.Once);
        _commandStore.Verify(store => store.RemoveTask(task), Times.Once);
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

    private DeleteProjectTaskHandler CreateHandler()
    {
        _cleanupQueue
            .Setup(queue => queue.PrepareTaskDeletionAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        return new(_access.Object, _commandStore.Object, _cleanupQueue.Object);
    }

    private void ConfigureTaskAccess(
        DeleteProjectTaskCommand command,
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

    private static (DeleteProjectTaskCommand Command, ProjectTask Task) CreateScenario(Guid? createdByUserId = null)
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
            createdByUserId ?? userId);

        return (
            new(userId, projectId, task.Id, task.ConcurrencyStamp),
            task);
    }
}
