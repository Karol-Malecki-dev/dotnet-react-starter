using API.Contracts.Projects;
using API.Modules.Projects;
using Application.Modules.Projects.ChangeProjectMemberRole;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;

namespace API.Modules.Projects.ChangeProjectMemberRole;

/// <summary>
/// HTTP adapter for the change-project-member-role vertical slice.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/members/{userId:guid}/role")]
[Authorize]
public sealed class ChangeProjectMemberRoleController : ProjectControllerBase
{
    private readonly IChangeProjectMemberRoleHandler _handler;

    public ChangeProjectMemberRoleController(IChangeProjectMemberRoleHandler handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Changes the role of an existing non-owner project member.
    /// </summary>
    [HttpPatch]
    public async Task<IActionResult> ChangeProjectMemberRole(
        Guid projectId,
        Guid userId,
        UpdateProjectMemberRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<ProjectMemberResponse>.Error(401, "User not authenticated"));
        }

        var result = await _handler.HandleAsync(
            new ChangeProjectMemberRoleCommand(ownerId, projectId, userId, request.Role),
            cancellationToken);

        return ToActionResult(result, MapMember);
    }
}
