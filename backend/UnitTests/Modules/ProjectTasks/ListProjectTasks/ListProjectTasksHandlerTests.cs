using Application.Features.ProjectManagement.Tasks;
using Application.Features.Projects;
using Application.Modules.ProjectTasks.ListProjectTasks;
using Domain.Enums;
using Infrastructure.Modules.ProjectTasks.ListProjectTasks;
using Moq;

namespace UnitTests.Modules.ProjectTasks.ListProjectTasks;

public sealed class ListProjectTasksHandlerTests
{
    private readonly Mock<IProjectTaskAccess> _access = new();
    private readonly Mock<IListProjectTasksQueryStore> _queryStore = new();

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
            store => store.QueryAsync(It.IsAny<ProjectTaskQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_returns_query_page_when_user_has_project_access()
    {
        var query = CreateQuery();
        var expected = new PagedProjectTaskView([], 1, 25, 0);
        _access
            .Setup(access => access.GetActiveProjectRoleAsync(
                query.UserId,
                query.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProjectMemberRole.Member);
        _queryStore
            .Setup(store => store.QueryAsync(
                It.IsAny<ProjectTaskQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await CreateHandler().HandleAsync(query);

        Assert.True(result.IsSuccess);
        Assert.Same(expected, result.Value);
    }

    private ListProjectTasksHandler CreateHandler()
        => new(_access.Object, _queryStore.Object);

    private static ProjectTaskQuery CreateQuery()
        => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            25,
            null,
            null,
            null,
            null,
            null,
            null,
            ProjectTaskSortBy.CreatedAt,
            SortDirection.Ascending);
}
