using Application.Features.ProjectManagement.Tasks;
using Application.Features.Projects;
using Application.Modules.ProjectTasks.Dashboard;
using Application.Modules.Projects.GetProjectDashboard;
using Domain.Enums;
using Infrastructure.Modules.Projects.GetProjectDashboard;
using Moq;

namespace UnitTests.Modules.Projects.GetProjectDashboard;

public sealed class GetProjectDashboardHandlerTests
{
    private readonly Mock<IGetProjectDashboardStore> _store = new();
    private readonly Mock<IProjectTaskDashboardReader> _taskReader = new();

    [Fact]
    public async Task Returns_not_found_without_reading_tasks_when_access_is_denied()
    {
        var query = new GetProjectDashboardQuery(Guid.NewGuid(), Guid.NewGuid());
        _store.Setup(candidate => candidate.HasProjectAccessAsync(
                query.UserId,
                query.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var handler = CreateHandler();

        var result = await handler.HandleAsync(query);

        Assert.Equal(ProjectOperationStatus.NotFound, result.Status);
        _taskReader.Verify(reader => reader.ReadAsync(
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Composes_task_snapshot_and_recent_activity_in_order()
    {
        var query = new GetProjectDashboardQuery(Guid.NewGuid(), Guid.NewGuid());
        var cancellationToken = new CancellationTokenSource().Token;
        var overdue = CreateTask(query.ProjectId, "Overdue");
        var upcoming = CreateTask(query.ProjectId, "Upcoming");
        var activity = new ProjectActivityView(
            Guid.NewGuid(),
            "project.updated",
            "updated the project.",
            query.UserId,
            "Owner",
            null,
            DateTime.UtcNow);
        var snapshot = new ProjectTaskDashboardSnapshot(
            3,
            1,
            1,
            1,
            1,
            1,
            1,
            [overdue],
            [upcoming]);
        _store.Setup(candidate => candidate.HasProjectAccessAsync(
                query.UserId,
                query.ProjectId,
                cancellationToken))
            .ReturnsAsync(true);
        _taskReader.Setup(reader => reader.ReadAsync(query.ProjectId, cancellationToken))
            .ReturnsAsync(snapshot);
        _store.Setup(candidate => candidate.GetRecentActivityAsync(
                query.ProjectId,
                5,
                cancellationToken))
            .ReturnsAsync([activity]);
        var handler = CreateHandler();

        var result = await handler.HandleAsync(query, cancellationToken);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(3, result.Value.TotalTasks);
        Assert.Same(overdue, Assert.Single(result.Value.OverdueTasks));
        Assert.Same(upcoming, Assert.Single(result.Value.UpcomingTasks));
        Assert.Same(activity, Assert.Single(result.Value.RecentActivities));
    }

    private GetProjectDashboardHandler CreateHandler()
        => new(_store.Object, _taskReader.Object);

    private static ProjectTaskView CreateTask(Guid projectId, string title)
        => new(
            Guid.NewGuid(),
            projectId,
            title,
            null,
            ProjectTaskStatus.Todo,
            ProjectTaskPriority.Normal,
            DateTime.UtcNow.Date,
            null,
            Guid.NewGuid(),
            DateTime.UtcNow,
            DateTime.UtcNow,
            Guid.NewGuid().ToString("N"),
            []);
}
