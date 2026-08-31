using API.Contracts.Projects;
using Application.Features.Projects;
using Application.Modules.Projects.DeclineProjectInvitation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;

namespace API.Modules.Projects.DeclineProjectInvitation;

/// <summary>
/// Exposes project invitation rejection.
/// </summary>
[ApiController]
[Authorize]
[Route("api/project-invitations/decline")]
public sealed class DeclineProjectInvitationController : ProjectControllerBase
{
    private readonly IDeclineProjectInvitationHandler _handler;

    public DeclineProjectInvitationController(IDeclineProjectInvitationHandler handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Declines a pending invitation addressed to the current user.
    /// </summary>
    /// <response code="200">The invitation was declined.</response>
    /// <response code="400">The invitation token is missing or invalid.</response>
    /// <response code="401">The request is unauthenticated.</response>
    /// <response code="404">The invitation does not exist or belongs to another user.</response>
    /// <response code="409">The invitation is expired, answered, or concurrently changed.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ProjectInvitationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Decline(
        [FromBody] RespondToProjectInvitationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<ProjectInvitationResponse>.Error(
                401,
                "User not authenticated"));
        }

        var result = await _handler.HandleAsync(
            new DeclineProjectInvitationCommand(userId, request.Token),
            cancellationToken);

        return ToActionResult(result, MapInvitation);
    }
}
