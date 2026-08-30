using API.Contracts.Projects;
using API.Modules.ProjectTasks;
using Application.Modules.ProjectTasks.ListProjectTasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;

namespace API.Modules.ProjectTasks.ListProjectTasks;

/// <summary>
/// HTTP adapter for the list-project-tasks vertical slice.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/tasks")]
[Authorize]
public sealed class ListProjectTasksController : ProjectTaskControllerBase
{
    private readonly IListProjectTasksHandler _handler;

    public ListProjectTasksController(IListProjectTasksHandler handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Lists tasks visible to the current user using the supported filters and ordering.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetTasks(
        Guid projectId,
        [FromQuery] ProjectTaskQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<PagedProjectTaskResponse>.Error(401, "User not authenticated"));
        }

        var result = await _handler.HandleAsync(
            new ProjectTaskQuery(
                userId,
                projectId,
                request.PageNumber,
                request.PageSize,
                request.Search,
                request.Status,
                request.Priority,
                request.AssignedUserId,
                request.Label,
                request.DueBefore,
                request.SortBy,
                request.SortDirection),
            cancellationToken);

        return ToActionResult(
            result,
            page => new PagedProjectTaskResponse(
                page.Items.Select(MapTask).ToList(),
                page.PageNumber,
                page.PageSize,
                page.TotalCount));
    }
}
