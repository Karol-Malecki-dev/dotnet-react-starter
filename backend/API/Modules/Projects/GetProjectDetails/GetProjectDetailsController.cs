using API.Contracts.Projects;
using API.Modules.Projects;
using Application.Modules.Projects.GetProjectDetails;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;

namespace API.Modules.Projects.GetProjectDetails;

/// <summary>
/// HTTP adapter for the get-project-details vertical slice.
/// </summary>
[ApiController]
[Route("api/projects")]
[Authorize]
public sealed class GetProjectDetailsController : ProjectControllerBase
{
    private readonly IGetProjectDetailsHandler _handler;

    public GetProjectDetailsController(IGetProjectDetailsHandler handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Gets a project visible to the current user.
    /// </summary>
    [HttpGet("{projectId:guid}")]
    public async Task<IActionResult> GetProject(
        Guid projectId,
        [FromQuery] bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<ProjectResponse>.Error(401, "User not authenticated"));
        }

        var result = await _handler.HandleAsync(
            new GetProjectDetailsQuery(userId, projectId, includeArchived),
            cancellationToken);

        return ToActionResult(result, MapProject);
    }
}
