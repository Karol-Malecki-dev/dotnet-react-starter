using API.Contracts.Projects;
using API.Modules.ProjectTasks;
using Application.Modules.ProjectTasks.UpdateProjectTaskStatus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;

namespace API.Modules.ProjectTasks.UpdateProjectTaskStatus;

/// <summary>
/// HTTP adapter for the update-project-task-status vertical slice.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/tasks")]
[Authorize]
public sealed class UpdateProjectTaskStatusController : ProjectTaskControllerBase
{
    private readonly IUpdateProjectTaskStatusHandler _handler;

    public UpdateProjectTaskStatusController(IUpdateProjectTaskStatusHandler handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Changes a task status when the current user can edit it and supplies its current concurrency stamp.
    /// </summary>
    [HttpPatch("{taskId:guid}/status")]
    public async Task<IActionResult> UpdateTaskStatus(
        Guid projectId,
        Guid taskId,
        UpdateProjectTaskStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<ProjectTaskResponse>.Error(401, "User not authenticated"));
        }

        var result = await _handler.HandleAsync(
            new UpdateProjectTaskStatusCommand(
                userId,
                projectId,
                taskId,
                request.Status,
                request.ConcurrencyStamp),
            cancellationToken);

        return ToActionResult(result, MapTask);
    }
}
