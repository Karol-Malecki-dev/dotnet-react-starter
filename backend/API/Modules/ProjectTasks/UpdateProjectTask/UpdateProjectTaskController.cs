using API.Contracts.Projects;
using API.Modules.ProjectTasks;
using Application.Modules.ProjectTasks.UpdateProjectTask;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;

namespace API.Modules.ProjectTasks.UpdateProjectTask;

/// <summary>
/// HTTP adapter for the update-project-task vertical slice.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/tasks")]
[Authorize]
public sealed class UpdateProjectTaskController : ProjectTaskControllerBase
{
    private readonly IUpdateProjectTaskHandler _handler;

    public UpdateProjectTaskController(IUpdateProjectTaskHandler handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Updates a task when the current user can edit it and supplies its current concurrency stamp.
    /// </summary>
    [HttpPut("{taskId:guid}")]
    public async Task<IActionResult> UpdateTask(
        Guid projectId,
        Guid taskId,
        UpdateProjectTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<ProjectTaskResponse>.Error(401, "User not authenticated"));
        }

        var result = await _handler.HandleAsync(
            new UpdateProjectTaskCommand(
                userId,
                projectId,
                taskId,
                request.Title,
                request.Description,
                request.Priority,
                request.DueDate,
                request.AssignedUserId,
                request.Labels ?? [],
                request.ConcurrencyStamp),
            cancellationToken);

        return ToActionResult(result, MapTask);
    }
}
