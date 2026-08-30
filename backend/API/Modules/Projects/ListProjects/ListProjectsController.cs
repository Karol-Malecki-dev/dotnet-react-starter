using API.Contracts.Projects;
using API.Modules.Projects;
using Application.Modules.Projects.ListProjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;

namespace API.Modules.Projects.ListProjects;

/// <summary>
/// HTTP adapter for the list-projects vertical slice.
/// </summary>
[ApiController]
[Route("api/projects")]
[Authorize]
public sealed class ListProjectsController : ProjectControllerBase
{
    private readonly IListProjectsHandler _handler;

    public ListProjectsController(IListProjectsHandler handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Lists projects visible to the current user.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetProjects(
        [FromQuery] bool includeArchived = false,
        [FromQuery] string scope = "all",
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<List<ProjectResponse>>.Error(401, "User not authenticated"));
        }

        var result = await _handler.HandleAsync(
            new ListProjectsQuery(userId, includeArchived, scope),
            cancellationToken);

        return ToActionResult(
            result,
            projects => projects.Select(MapProject).ToList());
    }
}
