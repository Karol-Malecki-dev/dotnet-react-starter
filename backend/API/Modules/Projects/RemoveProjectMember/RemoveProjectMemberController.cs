using API.Modules.Projects;
using Application.Modules.Projects.RemoveProjectMember;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;

namespace API.Modules.Projects.RemoveProjectMember;

/// <summary>
/// HTTP adapter for the remove-project-member vertical slice.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/members/{userId:guid}")]
[Authorize]
public sealed class RemoveProjectMemberController : ProjectControllerBase
{
    private readonly IRemoveProjectMemberHandler _handler;

    public RemoveProjectMemberController(IRemoveProjectMemberHandler handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Removes a non-owner member and unassigns their project tasks.
    /// </summary>
    [HttpDelete]
    public async Task<IActionResult> RemoveProjectMember(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<bool>.Error(401, "User not authenticated"));
        }

        var result = await _handler.HandleAsync(
            new RemoveProjectMemberCommand(ownerId, projectId, userId),
            cancellationToken);

        return ToActionResult(result, value => value);
    }
}
