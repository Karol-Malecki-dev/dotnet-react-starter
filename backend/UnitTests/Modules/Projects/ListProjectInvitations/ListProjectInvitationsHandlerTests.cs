using Application.Features.Projects;
using Application.Modules.Projects.ListMyProjectInvitations;
using Application.Modules.Projects.ListProjectInvitations;
using Domain.Enums;
using Infrastructure.Modules.Projects.ListMyProjectInvitations;
using Infrastructure.Modules.Projects.ListProjectInvitations;
using Moq;

namespace UnitTests.Modules.Projects.ListProjectInvitations;

public sealed class ListProjectInvitationsHandlerTests
{
    [Fact]
    public async Task Project_list_returns_not_found_when_project_is_not_owned()
    {
        var store = new Mock<IListProjectInvitationsStore>();
        var query = new ListProjectInvitationsQuery(Guid.NewGuid(), Guid.NewGuid());
        store.Setup(candidate => candidate.OwnedProjectExistsAsync(
                query.OwnerId,
                query.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var handler = new ListProjectInvitationsHandler(store.Object);

        var result = await handler.HandleAsync(query);

        Assert.Equal(ProjectOperationStatus.NotFound, result.Status);
        store.Verify(candidate => candidate.QueryAsync(
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Project_list_returns_store_projection_and_forwards_cancellation_token()
    {
        var store = new Mock<IListProjectInvitationsStore>();
        var query = new ListProjectInvitationsQuery(Guid.NewGuid(), Guid.NewGuid());
        var cancellationToken = new CancellationTokenSource().Token;
        IReadOnlyList<ProjectInvitationView> invitations =
        [
            CreateView(query.ProjectId, ProjectInvitationStatus.Accepted),
            CreateView(query.ProjectId, ProjectInvitationStatus.Pending)
        ];
        store.Setup(candidate => candidate.OwnedProjectExistsAsync(
                query.OwnerId,
                query.ProjectId,
                cancellationToken))
            .ReturnsAsync(true);
        store.Setup(candidate => candidate.QueryAsync(query.ProjectId, cancellationToken))
            .ReturnsAsync(invitations);
        var handler = new ListProjectInvitationsHandler(store.Object);

        var result = await handler.HandleAsync(query, cancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Same(invitations, result.Value);
    }

    [Fact]
    public async Task My_list_returns_store_projection_and_forwards_cancellation_token()
    {
        var store = new Mock<IListMyProjectInvitationsStore>();
        var query = new ListMyProjectInvitationsQuery(Guid.NewGuid());
        var cancellationToken = new CancellationTokenSource().Token;
        IReadOnlyList<ProjectInvitationView> invitations =
        [
            CreateView(Guid.NewGuid(), ProjectInvitationStatus.Pending)
        ];
        store.Setup(candidate => candidate.QueryAsync(query.UserId, cancellationToken))
            .ReturnsAsync(invitations);
        var handler = new ListMyProjectInvitationsHandler(store.Object);

        var result = await handler.HandleAsync(query, cancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Same(invitations, result.Value);
    }

    private static ProjectInvitationView CreateView(
        Guid projectId,
        ProjectInvitationStatus status)
        => new(
            Guid.NewGuid(),
            projectId,
            "Project",
            Guid.NewGuid(),
            "Recipient",
            "recipient@example.com",
            "Owner",
            ProjectMemberRole.Viewer,
            status,
            DateTime.UtcNow.AddDays(1),
            DateTime.UtcNow);
}
