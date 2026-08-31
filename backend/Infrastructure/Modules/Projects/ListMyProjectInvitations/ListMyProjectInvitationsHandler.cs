using Application.Features.Projects;
using Application.Modules.Projects.ListMyProjectInvitations;

namespace Infrastructure.Modules.Projects.ListMyProjectInvitations;

/// <summary>
/// Coordinates the current user's pending invitation projection.
/// </summary>
public sealed class ListMyProjectInvitationsHandler : IListMyProjectInvitationsHandler
{
    private readonly IListMyProjectInvitationsStore _store;

    public ListMyProjectInvitationsHandler(IListMyProjectInvitationsStore store)
    {
        _store = store;
    }

    public async Task<ProjectOperationResult<IReadOnlyList<ProjectInvitationView>>> HandleAsync(
        ListMyProjectInvitationsQuery query,
        CancellationToken cancellationToken = default)
    {
        var invitations = await _store.QueryAsync(query.UserId, cancellationToken);
        return ProjectOperationResult<IReadOnlyList<ProjectInvitationView>>.Success(invitations);
    }
}
