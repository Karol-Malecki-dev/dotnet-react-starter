using API.Modules.Projects;
using API.Contracts.Projects;
using Application.Modules.Projects.ListProjectMembers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;

namespace API.Modules.Projects.ListProjectMembers;

/// <summary>
/// HTTP adapter for the list-project-members vertical slice.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/members")]
[Authorize]
public sealed class ListProjectMembersController : ProjectControllerBase
{
    private readonly IListProjectMembersHandler _handler;

    public ListProjectMembersController(IListProjectMembersHandler handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Lists active members of a project visible to the current user.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListProjectMembers(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<List<ProjectMemberResponse>>.Error(401, "User not authenticated"));
        }

        var result = await _handler.HandleAsync(
            new ListProjectMembersQuery(userId, projectId),
            cancellationToken);

        return ToActionResult(result, members => members.Select(MapMember).ToList());
    }
}
