using Application.Features.ProjectManagement.Tasks;
using Application.Features.Projects;
using Application.Modules.ProjectTasks.Comments;
using Application.Modules.ProjectTasks.ListProjectTaskComments;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Modules.ProjectTasks.ListProjectTaskComments;
using Moq;

namespace UnitTests.Modules.ProjectTasks.ListProjectTaskComments;

public sealed class ListProjectTaskCommentsHandlerTests
{
    private readonly Mock<IProjectTaskAccess> _access = new();
    private readonly Mock<IListProjectTaskCommentsQueryStore> _queryStore = new();

    [Fact]
    public async Task Handle_returns_not_found_without_querying_when_user_has_no_project_access()
    {
        var query = CreateQuery();
        _access
            .Setup(access => access.GetActiveProjectRoleAsync(
                query.UserId,
                query.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProjectMemberRole?)null);

        var result = await CreateHandler().HandleAsync(query);

        Assert.Equal(ProjectOperationStatus.NotFound, result.Status);
        _queryStore.Verify(
            store => store.QueryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_returns_not_found_when_task_does_not_belong_to_project()
    {
        var query = CreateQuery();
        ConfigureAccess(query, null);

        var result = await CreateHandler().HandleAsync(query);

        Assert.Equal(ProjectOperationStatus.NotFound, result.Status);
        _queryStore.Verify(
            store => store.QueryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_returns_comments_for_an_accessible_task()
    {
        var query = CreateQuery();
        ConfigureAccess(query, ProjectTask.Create(query.ProjectId, "Task", null, ProjectTaskPriority.Normal, null, null, query.UserId));
        var expected = new List<ProjectTaskCommentView>
        {
            new(Guid.NewGuid(), query.TaskId, query.UserId, "Member", "Comment", DateTime.UtcNow)
        };
        _queryStore
            .Setup(store => store.QueryAsync(query.TaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await CreateHandler().HandleAsync(query);

        Assert.True(result.IsSuccess);
        Assert.Same(expected, result.Value);
        _queryStore.Verify(
            store => store.QueryAsync(query.TaskId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private ListProjectTaskCommentsHandler CreateHandler()
        => new(_access.Object, _queryStore.Object);

    private void ConfigureAccess(
        ListProjectTaskCommentsQuery query,
        ProjectTask? task)
    {
        _access
            .Setup(access => access.GetActiveProjectRoleAsync(
                query.UserId,
                query.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProjectMemberRole.Member);
        _access
            .Setup(access => access.GetTaskWithLabelsAsync(
                query.ProjectId,
                query.TaskId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);
    }

    private static ListProjectTaskCommentsQuery CreateQuery()
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
}
