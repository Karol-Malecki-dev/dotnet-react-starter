using Application.Modules.Workspace.SearchWorkspace;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;

namespace API.Modules.Workspace.SearchWorkspace;

[ApiController]
[Route("api/workspace/search")]
[Authorize]
public sealed class SearchWorkspaceController : ControllerBase
{
    private readonly ISearchWorkspaceHandler _handler;

    public SearchWorkspaceController(ISearchWorkspaceHandler handler)
    {
        _handler = handler;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<WorkspaceSearchPage>>> Search(
        [FromQuery] string? query,
        [FromQuery] string type = "projectTask",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var userId))
        {
            return Unauthorized(ApiResponse<WorkspaceSearchPage>.Error(401, "User not authenticated"));
        }

        var normalizedQuery = query?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedQuery) || normalizedQuery.Length is < 2 or > 100)
        {
            return BadRequest(ApiResponse<WorkspaceSearchPage>.Error(400, "Query must contain between 2 and 100 characters"));
        }

        var result = await _handler.HandleAsync(
            new SearchWorkspaceQuery(userId, normalizedQuery, type, page, pageSize),
            cancellationToken);

        return Ok(ApiResponse<WorkspaceSearchPage>.Success(result));
    }
}
