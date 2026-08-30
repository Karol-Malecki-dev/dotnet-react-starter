using Application.Features.ProjectManagement.Tasks;
using Application.Features.Projects;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.ProjectManagement.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace UnitTests.Services;

public sealed class ProjectTaskApplicationServiceTests
{
    private readonly Mock<IProjectTaskAccess> _access = new();
    private readonly Mock<IProjectTaskQueryStore> _queryStore = new();
    private readonly Mock<IProjectTaskCommandStore> _commandStore = new();
    private readonly Mock<INotificationService> _notificationService = new();

    [Fact]
    public async Task Query_returns_not_found_when_user_has_no_project_access()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _access.Setup(access => access.GetActiveProjectRoleAsync(userId, projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProjectMemberRole?)null);
        var service = new DatabaseProjectTaskQueryService(_access.Object, _queryStore.Object);

        var result = await service.GetProjectTasksAsync(CreateQuery(userId, projectId));

        Assert.Equal(ProjectOperationStatus.NotFound, result.Status);
        _queryStore.Verify(store => store.QueryAsync(It.IsAny<ProjectTaskQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Query_returns_store_page_when_user_has_project_access()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var expected = new PagedProjectTaskView([], 1, 25, 0);
        _access.Setup(access => access.GetActiveProjectRoleAsync(userId, projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProjectMemberRole.Member);
        _queryStore.Setup(store => store.QueryAsync(It.IsAny<ProjectTaskQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var service = new DatabaseProjectTaskQueryService(_access.Object, _queryStore.Object);

        var result = await service.GetProjectTasksAsync(CreateQuery(userId, projectId));

        Assert.True(result.IsSuccess);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task Update_returns_conflict_for_a_stale_task_concurrency_stamp_without_mutating()
    {
        var ownerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var task = ProjectTask.Create(projectId, "Current title", null, ProjectTaskPriority.Normal, null, null, ownerId);
        var command = new UpdateProjectTaskCommand(
            ownerId, projectId, task.Id, "Stale title", null, ProjectTaskPriority.High, null, null, [], "stale-stamp");
        _access.Setup(access => access.GetActiveProjectRoleAsync(ownerId, projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProjectMemberRole.Owner);
        _access.Setup(access => access.GetTaskWithLabelsAsync(projectId, task.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);
        var service = CreateCommandService();

        var result = await service.UpdateProjectTaskAsync(command);

        Assert.Equal(ProjectOperationStatus.Conflict, result.Status);
        Assert.Equal("Current title", task.Title);
        _commandStore.Verify(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_returns_conflict_when_persistence_detects_a_concurrent_task_change()
    {
        var ownerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var task = ProjectTask.Create(projectId, "Current title", null, ProjectTaskPriority.Normal, null, null, ownerId);
        var command = new UpdateProjectTaskCommand(
            ownerId, projectId, task.Id, "Updated title", null, ProjectTaskPriority.High, null, null, [], task.ConcurrencyStamp);
        _access.Setup(access => access.GetActiveProjectRoleAsync(ownerId, projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProjectMemberRole.Owner);
        _access.Setup(access => access.GetTaskWithLabelsAsync(projectId, task.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);
        _commandStore.Setup(store => store.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException());
        var service = CreateCommandService();

        var result = await service.UpdateProjectTaskAsync(command);

        Assert.Equal(ProjectOperationStatus.Conflict, result.Status);
        Assert.Contains("concurrently", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private DatabaseProjectTaskCommandService CreateCommandService()
        => new(_access.Object, _commandStore.Object, _notificationService.Object);

    private static ProjectTaskQuery CreateQuery(Guid userId, Guid projectId)
        => new(userId, projectId, 1, 25, null, null, null, null, null, null,
            ProjectTaskSortBy.CreatedAt, SortDirection.Ascending);

}