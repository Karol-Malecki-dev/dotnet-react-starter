using Application.Features.Projects;
using Application.Modules.Projects.GetProjectDetails;
using Domain.Enums;
using Infrastructure.Modules.Projects.GetProjectDetails;
using Moq;

namespace UnitTests.Modules.Projects.GetProjectDetails;

public sealed class GetProjectDetailsHandlerTests
{
    private readonly Mock<IGetProjectDetailsStore> _store = new();

    [Fact]
    public async Task Handle_returns_not_found_when_store_has_no_visible_project()
    {
        var query = CreateQuery();
        _store
            .Setup(store => store.QueryAsync(
                query.UserId,
                query.ProjectId,
                query.IncludeArchived,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProjectView?)null);

        var result = await CreateHandler().HandleAsync(query);

        Assert.Equal(ProjectOperationStatus.NotFound, result.Status);
        Assert.Equal("Project not found", result.Message);
    }

    [Fact]
    public async Task Handle_returns_project_details_from_store()
    {
        var query = CreateQuery();
        var project = new ProjectView(
            query.ProjectId,
            "Project name",
            "Project description",
            query.UserId,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow,
            "concurrency-stamp",
            false,
            ProjectMemberRole.Owner);
        _store
            .Setup(store => store.QueryAsync(
                query.UserId,
                query.ProjectId,
                query.IncludeArchived,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        var result = await CreateHandler().HandleAsync(query);

        Assert.True(result.IsSuccess);
        Assert.Equal(project, result.Value);
    }

    [Fact]
    public async Task Handle_forwards_the_cancellation_token_to_store()
    {
        var query = CreateQuery();
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        _store
            .Setup(store => store.QueryAsync(
                query.UserId,
                query.ProjectId,
                query.IncludeArchived,
                cancellationToken))
            .ReturnsAsync((ProjectView?)null);

        await CreateHandler().HandleAsync(query, cancellationToken);

        _store.Verify(store => store.QueryAsync(
            query.UserId,
            query.ProjectId,
            query.IncludeArchived,
            cancellationToken), Times.Once);
    }

    private GetProjectDetailsHandler CreateHandler()
        => new(_store.Object);

    private static GetProjectDetailsQuery CreateQuery()
        => new(Guid.NewGuid(), Guid.NewGuid(), IncludeArchived: false);
}
