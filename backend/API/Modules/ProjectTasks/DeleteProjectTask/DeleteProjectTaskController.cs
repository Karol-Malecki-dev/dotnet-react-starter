using API.Modules.ProjectTasks;
using Application.Modules.ProjectTasks.DeleteProjectTask;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;

namespace API.Modules.ProjectTasks.DeleteProjectTask;

/// <summary>
/// HTTP adapter for the delete-project-task vertical slice.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/tasks")]
[Authorize]
public sealed class DeleteProjectTaskController : ProjectTaskControllerBase
{
    private readonly IDeleteProjectTaskHandler _handler;

    public DeleteProjectTaskController(IDeleteProjectTaskHandler handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Deletes a task when the current user can edit it and supplies its current concurrency stamp.
    /// </summary>
    [HttpDelete("{taskId:guid}")]
    public async Task<IActionResult> DeleteTask(
        Guid projectId,
        Guid taskId,
        [FromQuery] string? concurrencyStamp = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<bool>.Error(401, "User not authenticated"));
        }

        var result = await _handler.HandleAsync(
            new DeleteProjectTaskCommand(
                userId,
                projectId,
                taskId,
                concurrencyStamp),
            cancellationToken);

        return ToActionResult(result, value => value);
    }
}
