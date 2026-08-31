using Application.Features.Projects;
using Application.Modules.Projects.ListAvailableProjectMembers;
using Infrastructure.Modules.Projects.ListAvailableProjectMembers;
using Moq;

namespace UnitTests.Modules.Projects.ListAvailableProjectMembers;

public sealed class ListAvailableProjectMembersHandlerTests
{
    private readonly Mock<IListAvailableProjectMembersStore> _store = new();

    [Fact]
    public async Task Handle_returns_not_found_without_querying_users_when_project_is_not_owned_by_user()
    {
        var query = CreateQuery();
        _store
            .Setup(store => store.OwnedProjectExistsAsync(
                query.OwnerId,
                query.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await CreateHandler().HandleAsync(query);

        Assert.Equal(ProjectOperationStatus.NotFound, result.Status);
        _store.Verify(store => store.QueryAsync(
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_returns_available_users_in_store_order_when_project_is_owned_by_user()
    {
        var query = CreateQuery();
        var users = new List<ProjectMemberUserView>
        {
            new(Guid.NewGuid(), "First user", "first@example.com"),
            new(Guid.NewGuid(), "Second user", "second@example.com")
        };
        _store
            .Setup(store => store.OwnedProjectExistsAsync(
                query.OwnerId,
                query.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _store
            .Setup(store => store.QueryAsync(query.ProjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        var result = await CreateHandler().HandleAsync(query);

        Assert.True(result.IsSuccess);
        Assert.Equal(users, result.Value);
        _store.Verify(store => store.QueryAsync(
            query.ProjectId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_forwards_cancellation_token_to_both_store_operations()
    {
        var query = CreateQuery();
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        _store
            .Setup(store => store.OwnedProjectExistsAsync(
                query.OwnerId,
                query.ProjectId,
                cancellationToken))
            .ReturnsAsync(true);
        _store
            .Setup(store => store.QueryAsync(query.ProjectId, cancellationToken))
            .ReturnsAsync(Array.Empty<ProjectMemberUserView>());

        await CreateHandler().HandleAsync(query, cancellationToken);

        _store.Verify(store => store.OwnedProjectExistsAsync(
            query.OwnerId,
            query.ProjectId,
            cancellationToken), Times.Once);
        _store.Verify(store => store.QueryAsync(query.ProjectId, cancellationToken), Times.Once);
    }

    private ListAvailableProjectMembersHandler CreateHandler()
        => new(_store.Object);

    private static ListAvailableProjectMembersQuery CreateQuery()
        => new(Guid.NewGuid(), Guid.NewGuid());
}
