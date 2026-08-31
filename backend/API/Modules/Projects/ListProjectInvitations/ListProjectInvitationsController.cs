using API.Contracts.Projects;
using Application.Features.Projects;
using Application.Modules.Projects.ListProjectInvitations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;

namespace API.Modules.Projects.ListProjectInvitations;

/// <summary>
/// Exposes the owner-only project invitation list.
/// </summary>
[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/invitations")]
public sealed class ListProjectInvitationsController : ProjectControllerBase
{
    private readonly IListProjectInvitationsHandler _handler;

    public ListProjectInvitationsController(IListProjectInvitationsHandler handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Returns invitations created for a project owned by the current user.
    /// </summary>
    /// <response code="200">Returns the project invitations.</response>
    /// <response code="401">The request is unauthenticated.</response>
    /// <response code="404">The project does not exist or is not owned by the current user.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ProjectInvitationResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> List(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<IReadOnlyList<ProjectInvitationResponse>>.Error(
                401,
                "User not authenticated"));
        }

        var result = await _handler.HandleAsync(
            new ListProjectInvitationsQuery(ownerId, projectId),
            cancellationToken);

        return ToActionResult(
            result,
            invitations => (IReadOnlyList<ProjectInvitationResponse>)invitations
                .Select(MapInvitation)
                .ToList());
    }
}
