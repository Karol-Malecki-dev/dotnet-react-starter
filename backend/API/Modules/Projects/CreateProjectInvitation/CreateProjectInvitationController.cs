using API.Contracts.Projects;
using Application.Modules.Projects.CreateProjectInvitation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;

namespace API.Modules.Projects.CreateProjectInvitation;

/// <summary>
/// Exposes project invitation creation.
/// </summary>
[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/invitations")]
public sealed class CreateProjectInvitationController : ProjectControllerBase
{
    private readonly ICreateProjectInvitationHandler _handler;

    public CreateProjectInvitationController(ICreateProjectInvitationHandler handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Creates a seven-day invitation for an existing active user.
    /// </summary>
    /// <remarks>
    /// The raw invitation token is returned only by this response. Only project owners
    /// can create invitations, and the invited user cannot already be a project member.
    /// </remarks>
    /// <response code="201">The invitation was created.</response>
    /// <response code="400">The email or role is invalid.</response>
    /// <response code="401">The request is unauthenticated.</response>
    /// <response code="404">The project or active recipient was not found.</response>
    /// <response code="409">The user is already a member or has a pending invitation.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CreatedProjectInvitationResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        Guid projectId,
        [FromBody] CreateProjectInvitationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<CreatedProjectInvitationResponse>.Error(
                401,
                "User not authenticated"));
        }

        var result = await _handler.HandleAsync(
            new CreateProjectInvitationCommand(
                ownerId,
                projectId,
                request.Email,
                request.Role),
            cancellationToken);

        return ToActionResult(
            result,
            created => new CreatedProjectInvitationResponse(
                MapInvitation(created.Invitation),
                created.Token));
    }
}
