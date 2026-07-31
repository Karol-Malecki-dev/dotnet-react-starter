using API.Contracts.Projects;
using Application.Features.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace API.Controllers;

/// <summary>
/// Manages secure, time-limited project invitations.
/// </summary>
[ApiController]
[Route("api")]
[Authorize]
public sealed class ProjectInvitationsController : ControllerBase
{
    private readonly IProjectApplicationService _projectService;

    public ProjectInvitationsController(IProjectApplicationService projectService)
    {
        _projectService = projectService;
    }

    /// <summary>Returns all invitations for a project. Only the project owner can use this endpoint.</summary>
    [HttpGet("projects/{projectId:guid}/invitations")]
    public async Task<IActionResult> GetProjectInvitations(Guid projectId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<IReadOnlyList<ProjectInvitationResponse>>.Error(401, "User not authenticated"));
        }

        var result = await _projectService.GetProjectInvitationsAsync(userId, projectId);
        return ToActionResult(result, invitations => invitations.Select(MapInvitation).ToList());
    }

    /// <summary>Creates a seven-day invitation for an active, non-member account.</summary>
    [HttpPost("projects/{projectId:guid}/invitations")]
    public async Task<IActionResult> CreateProjectInvitation(Guid projectId, CreateProjectInvitationRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<CreatedProjectInvitationResponse>.Error(401, "User not authenticated"));
        }

        var result = await _projectService.CreateProjectInvitationAsync(new CreateProjectInvitationCommand(userId, projectId, request.Email, request.Role));
        return ToActionResult(result, created => new CreatedProjectInvitationResponse(MapInvitation(created.Invitation), created.Token));
    }

    /// <summary>Returns outstanding invitations for the authenticated recipient.</summary>
    [HttpGet("project-invitations/mine")]
    public async Task<IActionResult> GetMyProjectInvitations()
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<IReadOnlyList<ProjectInvitationResponse>>.Error(401, "User not authenticated"));
        }

        var result = await _projectService.GetMyProjectInvitationsAsync(userId);
        return ToActionResult(result, invitations => invitations.Select(MapInvitation).ToList());
    }

    /// <summary>Accepts an invitation when the logged-in user is its intended recipient.</summary>
    [HttpPost("project-invitations/accept")]
    public async Task<IActionResult> AcceptProjectInvitation(RespondToProjectInvitationRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<ProjectInvitationResponse>.Error(401, "User not authenticated"));
        }

        var result = await _projectService.AcceptProjectInvitationAsync(userId, request.Token);
        return ToActionResult(result, MapInvitation);
    }

    /// <summary>Declines an invitation when the logged-in user is its intended recipient.</summary>
    [HttpPost("project-invitations/decline")]
    public async Task<IActionResult> DeclineProjectInvitation(RespondToProjectInvitationRequest request)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<ProjectInvitationResponse>.Error(401, "User not authenticated"));
        }

        var result = await _projectService.DeclineProjectInvitationAsync(userId, request.Token);
        return ToActionResult(result, MapInvitation);
    }

    private IActionResult ToActionResult<TValue, TResponse>(ProjectOperationResult<TValue> result, Func<TValue, TResponse> map)
    {
        if (!result.IsSuccess)
        {
            var statusCode = result.Status switch
            {
                ProjectOperationStatus.NotFound => 404,
                ProjectOperationStatus.ValidationError => 400,
                ProjectOperationStatus.Conflict => 409,
                ProjectOperationStatus.Forbidden => 403,
                _ => 500
            };
            return StatusCode(statusCode, ApiResponse<TResponse>.Error(statusCode, result.Message));
        }

        return StatusCode(result.CreatedStatusCode,
            ApiResponse<TResponse>.Success(map(result.Value!), result.Message, result.CreatedStatusCode));
    }

    private static ProjectInvitationResponse MapInvitation(ProjectInvitationView invitation) => new(
        invitation.Id,
        invitation.ProjectId,
        invitation.ProjectName,
        invitation.InvitedUserId,
        invitation.InvitedUserDisplayName,
        invitation.InvitedUserEmail,
        invitation.InvitedByDisplayName,
        invitation.Role,
        invitation.Status,
        invitation.ExpiresAt,
        invitation.CreatedAt);

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var userIdValue = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdValue, out userId);
    }
}
