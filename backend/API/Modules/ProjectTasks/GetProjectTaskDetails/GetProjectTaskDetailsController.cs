using API.Contracts.Projects;
using API.Modules.ProjectTasks;
using Application.Modules.ProjectTasks.GetProjectTaskDetails;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;

namespace API.Modules.ProjectTasks.GetProjectTaskDetails;

/// <summary>
/// HTTP adapter for the get-project-task-details vertical slice.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/tasks")]
[Authorize]
public sealed class GetProjectTaskDetailsController : ProjectTaskControllerBase
{
    private readonly IGetProjectTaskDetailsHandler _handler;

    public GetProjectTaskDetailsController(IGetProjectTaskDetailsHandler handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Returns one task when the current user can access the active project.
    /// </summary>
    [HttpGet("{taskId:guid}")]
    public async Task<IActionResult> GetTaskDetails(
        Guid projectId,
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<ProjectTaskResponse>.Error(401, "User not authenticated"));
        }

        var result = await _handler.HandleAsync(
            new GetProjectTaskDetailsQuery(userId, projectId, taskId),
            cancellationToken);

        return ToActionResult(result, MapTask);
    }
}
