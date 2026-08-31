using Application.Features.Projects;
using Application.Modules.Projects.GetProjectActivity;
using Infrastructure.Modules.Projects.GetProjectActivity;
using Moq;

namespace UnitTests.Modules.Projects.GetProjectActivity;

public sealed class GetProjectActivityHandlerTests
{
    private readonly Mock<IGetProjectActivityStore> _store = new();

    [Fact]
    public async Task Returns_not_found_without_querying_activity_when_access_is_denied()
    {
        var query = new GetProjectActivityQuery(Guid.NewGuid(), Guid.NewGuid(), 1, 20);
        _store.Setup(candidate => candidate.HasProjectAccessAsync(
                query.UserId,
                query.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var handler = new GetProjectActivityHandler(_store.Object);

        var result = await handler.HandleAsync(query);

        Assert.Equal(ProjectOperationStatus.NotFound, result.Status);
        _store.Verify(candidate => candidate.QueryAsync(
            It.IsAny<Guid>(),
            It.IsAny<int>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(-5, 101, 1, 100)]
    [InlineData(2, 25, 2, 25)]
    public async Task Normalizes_pagination_before_querying_store(
        int requestedPage,
        int requestedSize,
        int expectedPage,
        int expectedSize)
    {
        var query = new GetProjectActivityQuery(
            Guid.NewGuid(),
            Guid.NewGuid(),
            requestedPage,
            requestedSize);
        var cancellationToken = new CancellationTokenSource().Token;
        var page = new PagedProjectActivityView([], expectedPage, expectedSize, 0);
        _store.Setup(candidate => candidate.HasProjectAccessAsync(
                query.UserId,
                query.ProjectId,
                cancellationToken))
            .ReturnsAsync(true);
        _store.Setup(candidate => candidate.QueryAsync(
                query.ProjectId,
                expectedPage,
                expectedSize,
                cancellationToken))
            .ReturnsAsync(page);
        var handler = new GetProjectActivityHandler(_store.Object);

        var result = await handler.HandleAsync(query, cancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Same(page, result.Value);
    }
}
