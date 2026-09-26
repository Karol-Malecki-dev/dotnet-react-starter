using Application.Features.Projects;
using MediatR;
using Application.Modules.Projects.ListMyProjectInvitations;

namespace Infrastructure.Modules.Projects.ListMyProjectInvitations;

/// <summary>
/// Coordinates the current user's pending invitation projection.
/// </summary>
public sealed class ListMyProjectInvitationsHandler : IRequestHandler<ListMyProjectInvitationsQuery, ProjectOperationResult<IReadOnlyList<ProjectInvitationView>>>
{
    private readonly IListMyProjectInvitationsStore _store;

    public ListMyProjectInvitationsHandler(IListMyProjectInvitationsStore store)
    {
        _store = store;
    }

    public async Task<ProjectOperationResult<IReadOnlyList<ProjectInvitationView>>> Handle(
        ListMyProjectInvitationsQuery request,
        CancellationToken cancellationToken = default)
    {
        var invitations = await _store.QueryAsync(request.UserId, cancellationToken);
        return ProjectOperationResult<IReadOnlyList<ProjectInvitationView>>.Success(invitations);
    }
}
