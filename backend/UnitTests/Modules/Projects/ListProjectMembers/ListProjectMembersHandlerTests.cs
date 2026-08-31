using Application.Features.Projects;
using Application.Modules.Projects.ListProjectMembers;
using Domain.Enums;
using Infrastructure.Modules.Projects.ListProjectMembers;
using Moq;

namespace UnitTests.Modules.Projects.ListProjectMembers;

public sealed class ListProjectMembersHandlerTests
{
    private readonly Mock<IListProjectMembersStore> _store = new();

    [Fact]
    public async Task Handle_returns_not_found_without_querying_members_when_user_has_no_access()
    {
        var query = CreateQuery();
        _store
            .Setup(store => store.HasProjectAccessAsync(
                query.UserId,
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
    public async Task Handle_returns_members_in_store_order_when_user_has_access()
    {
        var query = CreateQuery();
        var members = new List<ProjectMemberView>
        {
            new(Guid.NewGuid(), "First member", "first@example.com", ProjectMemberRole.Member, DateTime.UtcNow),
            new(Guid.NewGuid(), "Second member", "second@example.com", ProjectMemberRole.Viewer, DateTime.UtcNow)
        };
        _store
            .Setup(store => store.HasProjectAccessAsync(
                query.UserId,
                query.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _store
            .Setup(store => store.QueryAsync(query.ProjectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(members);

        var result = await CreateHandler().HandleAsync(query);

        Assert.True(result.IsSuccess);
        Assert.Equal(members, result.Value);
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
            .Setup(store => store.HasProjectAccessAsync(
                query.UserId,
                query.ProjectId,
                cancellationToken))
            .ReturnsAsync(true);
        _store
            .Setup(store => store.QueryAsync(query.ProjectId, cancellationToken))
            .ReturnsAsync(Array.Empty<ProjectMemberView>());

        await CreateHandler().HandleAsync(query, cancellationToken);

        _store.Verify(store => store.HasProjectAccessAsync(
            query.UserId,
            query.ProjectId,
            cancellationToken), Times.Once);
        _store.Verify(store => store.QueryAsync(query.ProjectId, cancellationToken), Times.Once);
    }

    private ListProjectMembersHandler CreateHandler()
        => new(_store.Object);

    private static ListProjectMembersQuery CreateQuery()
        => new(Guid.NewGuid(), Guid.NewGuid());
}
