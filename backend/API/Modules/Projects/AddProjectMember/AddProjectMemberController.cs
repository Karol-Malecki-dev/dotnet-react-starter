using API.Contracts.Projects;
using API.Modules.Projects;
using Application.Modules.Projects.AddProjectMember;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;

namespace API.Modules.Projects.AddProjectMember;

/// <summary>
/// HTTP adapter for the add-project-member vertical slice.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/members")]
[Authorize]
public sealed class AddProjectMemberController : ProjectControllerBase
{
    private readonly IAddProjectMemberHandler _handler;

    public AddProjectMemberController(IAddProjectMemberHandler handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Adds an active user to a project owned by the current user.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> AddProjectMember(
        Guid projectId,
        AddProjectMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<ProjectMemberResponse>.Error(401, "User not authenticated"));
        }

        var result = await _handler.HandleAsync(
            new AddProjectMemberCommand(ownerId, projectId, request.UserId),
            cancellationToken);

        return ToActionResult(result, MapMember);
    }
}
