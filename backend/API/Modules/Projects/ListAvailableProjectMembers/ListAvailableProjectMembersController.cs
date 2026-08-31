using API.Contracts.Projects;
using API.Modules.Projects;
using Application.Modules.Projects.ListAvailableProjectMembers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;

namespace API.Modules.Projects.ListAvailableProjectMembers;

/// <summary>
/// HTTP adapter for the list-available-project-members vertical slice.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/members/available")]
[Authorize]
public sealed class ListAvailableProjectMembersController : ProjectControllerBase
{
    private readonly IListAvailableProjectMembersHandler _handler;

    public ListAvailableProjectMembersController(IListAvailableProjectMembersHandler handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Lists active users who are not currently members of a project owned by the current user.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListAvailableProjectMembers(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<List<ProjectMemberUserResponse>>.Error(401, "User not authenticated"));
        }

        var result = await _handler.HandleAsync(
            new ListAvailableProjectMembersQuery(ownerId, projectId),
            cancellationToken);

        return ToActionResult(result, users => users.Select(MapMemberUser).ToList());
    }
}
