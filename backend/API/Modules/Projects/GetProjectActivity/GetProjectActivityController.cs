using Application.Features.Projects;
using Application.Modules.Projects.GetProjectActivity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;

namespace API.Modules.Projects.GetProjectActivity;

/// <summary>
/// Exposes the paged project activity timeline.
/// </summary>
[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/activity")]
public sealed class GetProjectActivityController : ProjectControllerBase
{
    private readonly ISender _sender;

    public GetProjectActivityController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Returns project activity in reverse chronological order.
    /// </summary>
    /// <param name="pageNumber">One-based page number. Values below one are normalized to one.</param>
    /// <param name="pageSize">Requested page size, normalized to the range 1 through 100.</param>
    /// <response code="200">Returns one page of activity.</response>
    /// <response code="401">The request is unauthenticated.</response>
    /// <response code="404">The project does not exist or is not visible to the current user.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedProjectActivityView>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        Guid projectId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<PagedProjectActivityView>.Error(
                401,
                "User not authenticated"));
        }

        var result = await _sender.Send(
            new GetProjectActivityQuery(userId, projectId, pageNumber, pageSize),
            cancellationToken);
        return ToActionResult(result, page => page);
    }
}
