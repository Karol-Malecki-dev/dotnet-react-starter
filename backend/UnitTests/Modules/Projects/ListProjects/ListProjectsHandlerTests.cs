using Application.Features.Projects;
using Application.Modules.Projects.ListProjects;
using Domain.Enums;
using Infrastructure.Modules.Projects.ListProjects;
using Moq;

namespace UnitTests.Modules.Projects.ListProjects;

public sealed class ListProjectsHandlerTests
{
    private readonly Mock<IListProjectsStore> _store = new();

    [Fact]
    public async Task Handle_returns_empty_success_when_store_has_no_visible_projects()
    {
        var query = CreateQuery();
        var expected = Array.Empty<ProjectView>();
        _store
            .Setup(store => store.QueryAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await CreateHandler().HandleAsync(query);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task Handle_returns_projects_in_the_order_provided_by_store()
    {
        var query = CreateQuery();
        var expected = new[]
        {
            CreateProjectView("Newest project"),
            CreateProjectView("Older project")
        };
        _store
            .Setup(store => store.QueryAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await CreateHandler().HandleAsync(query);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task Handle_forwards_the_full_query_and_cancellation_token_to_store()
    {
        var query = CreateQuery(includeArchived: true, scope: "member");
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        _store
            .Setup(store => store.QueryAsync(query, cancellationToken))
            .ReturnsAsync(Array.Empty<ProjectView>());

        await CreateHandler().HandleAsync(query, cancellationToken);

        _store.Verify(store => store.QueryAsync(query, cancellationToken), Times.Once);
    }

    private ListProjectsHandler CreateHandler()
        => new(_store.Object);

    private static ListProjectsQuery CreateQuery(bool includeArchived = false, string scope = "all")
        => new(Guid.NewGuid(), includeArchived, scope);

    private static ProjectView CreateProjectView(string name)
        => new(
            Guid.NewGuid(),
            name,
            null,
            Guid.NewGuid(),
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow,
            Guid.NewGuid().ToString("N"),
            false,
            ProjectMemberRole.Owner);
}
