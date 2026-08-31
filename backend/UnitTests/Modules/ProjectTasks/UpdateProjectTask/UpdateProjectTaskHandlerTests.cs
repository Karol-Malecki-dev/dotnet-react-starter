using Application.Features.ProjectManagement.Tasks;
using Application.Features.Projects;
using Application.Modules.ProjectTasks.UpdateProjectTask;
using Application.Modules.ProjectTasks.AssignmentNotifications;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Modules.ProjectTasks.UpdateProjectTask;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace UnitTests.Modules.ProjectTasks.UpdateProjectTask;

public sealed class UpdateProjectTaskHandlerTests
{
    private readonly Mock<IProjectTaskAccess> _access = new();
    private readonly Mock<IProjectTaskCommandStore> _commandStore = new();
    private readonly Mock<IProjectTaskAssignmentNotificationWriter> _assignmentNotificationWriter = new();

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
        _access
            .Setup(access => access.GetActiveProjectRoleAsync(
                command.UserId,
                command.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProjectMemberRole.Viewer);
        _access
            .Setup(access => access.GetTaskWithLabelsAsync(
                command.ProjectId,
                command.TaskId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.Forbidden, result.Status);
        _commandStore.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_returns_forbidden_for_member_who_did_not_create_task()
    {
        var (command, task) = CreateScenario(Guid.NewGuid());
        _access
            .Setup(access => access.GetActiveProjectRoleAsync(
                command.UserId,
                command.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProjectMemberRole.Member);
        _access
            .Setup(access => access.GetTaskWithLabelsAsync(
                command.ProjectId,
                command.TaskId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.Forbidden, result.Status);
        _commandStore.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_returns_validation_error_when_concurrency_stamp_is_missing()
    {
        var (scenarioCommand, task) = CreateScenario();
        var command = scenarioCommand with { ExpectedConcurrencyStamp = null };
        ConfigureAuthorizedOwner(command, task);

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.ValidationError, result.Status);
        Assert.Equal("Project task concurrency stamp is required", result.Message);
        Assert.Equal("Task title", task.Title);
        _commandStore.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_returns_conflict_for_a_stale_concurrency_stamp_without_mutating()
    {
        var (scenarioCommand, task) = CreateScenario();
        var command = scenarioCommand with { ExpectedConcurrencyStamp = "stale-stamp" };
        ConfigureAuthorizedOwner(command, task);

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.Conflict, result.Status);
        Assert.Equal("Task title", task.Title);
        _commandStore.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_returns_validation_error_for_an_inactive_assignee_without_mutating()
    {
        var (scenarioCommand, task) = CreateScenario();
        var command = scenarioCommand with
        {
            AssignedUserId = Guid.NewGuid()
        };
        ConfigureAuthorizedOwner(command, task);
        _commandStore
            .Setup(store => store.IsActiveProjectMemberAsync(
                command.ProjectId,
                command.AssignedUserId!.Value,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.ValidationError, result.Status);
        Assert.Equal("Assigned user is not an active member of this project", result.Message);
        Assert.Equal("Task title", task.Title);
        _commandStore.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_updates_task_and_notifies_a_new_assignee()
    {
        var assignedUserId = Guid.NewGuid();
        var (scenarioCommand, task) = CreateScenario();
        var command = scenarioCommand with
        {
            Title = "Updated title",
            Description = "Updated description",
            Priority = ProjectTaskPriority.High,
            DueDate = DateTime.UtcNow.AddDays(3),
            AssignedUserId = assignedUserId,
            Labels = ["new-label"]
        };
        ConfigureAuthorizedOwner(command, task);
        _commandStore
            .Setup(store => store.IsActiveProjectMemberAsync(
                command.ProjectId,
                assignedUserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateHandler().HandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal("Project task updated", result.Message);
        Assert.NotNull(result.Value);
        Assert.Equal(command.Title, result.Value.Title);
        Assert.Equal(command.Description, result.Value.Description);
        Assert.Equal(command.Priority, result.Value.Priority);
        Assert.Equal(command.AssignedUserId, result.Value.AssignedUserId);
        Assert.Equal(command.Labels, result.Value.Labels);
        _commandStore.Verify(store => store.ReplaceTaskLabels(
            task,
            It.Is<IReadOnlyCollection<ProjectTaskLabel>>(labels => labels.Count == 0)), Times.Once);
        _commandStore.Verify(store => store.AddActivity(It.Is<ProjectActivity>(activity =>
            activity.Type == "task.assigned"
            && activity.ActorUserId == command.UserId)), Times.Once);
        _commandStore.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _assignmentNotificationWriter.Verify(writer => writer.AddTaskAssignedNotificationAsync(
            assignedUserId,
            command.ProjectId,
            task.Id,
            command.Title,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_does_not_persist_when_assignment_notification_preparation_fails()
    {
        var assignedUserId = Guid.NewGuid();
        var (scenarioCommand, task) = CreateScenario();
        var command = scenarioCommand with { AssignedUserId = assignedUserId };
        ConfigureAuthorizedOwner(command, task);
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
                task.Id,
                command.Title,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("notification preparation failed"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateHandler().HandleAsync(command));

        _commandStore.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_returns_conflict_when_persistence_detects_a_concurrent_change()
    {
        var (command, task) = CreateScenario();
        ConfigureAuthorizedOwner(command, task);
        _commandStore
            .Setup(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException());

        var result = await CreateHandler().HandleAsync(command);

        Assert.Equal(ProjectOperationStatus.Conflict, result.Status);
        _commandStore.Verify(store => store.ClearChangeTracker(), Times.Once);
    }

    private UpdateProjectTaskHandler CreateHandler()
        => new(_access.Object, _commandStore.Object, _assignmentNotificationWriter.Object);

    private void ConfigureAuthorizedOwner(UpdateProjectTaskCommand command, ProjectTask task)
    {
        _access
            .Setup(access => access.GetActiveProjectRoleAsync(
                command.UserId,
                command.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProjectMemberRole.Owner);
        _access
            .Setup(access => access.GetTaskWithLabelsAsync(
                command.ProjectId,
                command.TaskId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);
    }

    private static (UpdateProjectTaskCommand Command, ProjectTask Task) CreateScenario(Guid? createdByUserId = null)
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var task = CreateTask(projectId, createdByUserId ?? userId);
        return (new(
            userId,
            projectId,
            task.Id,
            "Task title",
            "Description",
            ProjectTaskPriority.Normal,
            null,
            null,
            [],
            task.ConcurrencyStamp), task);
    }

    private static ProjectTask CreateTask(Guid projectId, Guid createdByUserId)
        => ProjectTask.Create(
            projectId,
            "Task title",
            "Description",
            ProjectTaskPriority.Normal,
            null,
            null,
            createdByUserId);
}
