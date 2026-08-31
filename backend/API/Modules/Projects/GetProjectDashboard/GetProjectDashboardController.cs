using Application.Features.Projects;
using Application.Modules.Projects.GetProjectDashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;

namespace API.Modules.Projects.GetProjectDashboard;

/// <summary>
/// Exposes the composed project dashboard.
/// </summary>
[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/dashboard")]
public sealed class GetProjectDashboardController : ProjectControllerBase
{
    private readonly IGetProjectDashboardHandler _handler;

    public GetProjectDashboardController(IGetProjectDashboardHandler handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Returns task metrics, due-date lists, and recent project activity.
    /// </summary>
    /// <response code="200">Returns the project dashboard.</response>
    /// <response code="401">The request is unauthenticated.</response>
    /// <response code="404">The project does not exist or is not visible to the current user.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<ProjectDashboardView>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<ProjectDashboardView>.Error(
                401,
                "User not authenticated"));
        }

        var result = await _handler.HandleAsync(
            new GetProjectDashboardQuery(userId, projectId),
            cancellationToken);
        return ToActionResult(result, dashboard => dashboard);
    }
}
