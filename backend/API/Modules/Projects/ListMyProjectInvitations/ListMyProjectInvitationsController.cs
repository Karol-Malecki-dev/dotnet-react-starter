using API.Contracts.Projects;
using Application.Features.Projects;
using Application.Modules.Projects.ListMyProjectInvitations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;

namespace API.Modules.Projects.ListMyProjectInvitations;

/// <summary>
/// Exposes invitations addressed to the current user.
/// </summary>
[ApiController]
[Authorize]
[Route("api/project-invitations/mine")]
public sealed class ListMyProjectInvitationsController : ProjectControllerBase
{
    private readonly IListMyProjectInvitationsHandler _handler;

    public ListMyProjectInvitationsController(IListMyProjectInvitationsHandler handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Returns pending invitations addressed to the current user.
    /// </summary>
    /// <response code="200">Returns the current user's invitations.</response>
    /// <response code="401">The request is unauthenticated.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ProjectInvitationResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<IReadOnlyList<ProjectInvitationResponse>>.Error(
                401,
                "User not authenticated"));
        }

        var result = await _handler.HandleAsync(
            new ListMyProjectInvitationsQuery(userId),
            cancellationToken);

        return ToActionResult(
            result,
            invitations => (IReadOnlyList<ProjectInvitationResponse>)invitations
                .Select(MapInvitation)
                .ToList());
    }
}
