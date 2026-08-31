using API.Modules.ProjectTasks;
using Application.Modules.ProjectTasks.DeleteProjectTaskComment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;

namespace API.Modules.ProjectTasks.DeleteProjectTaskComment;

/// <summary>
/// HTTP adapter for the delete-project-task-comment vertical slice.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/tasks/{taskId:guid}/comments")]
[Authorize]
public sealed class DeleteProjectTaskCommentController : ProjectTaskControllerBase
{
    private readonly IDeleteProjectTaskCommentHandler _handler;

    public DeleteProjectTaskCommentController(IDeleteProjectTaskCommentHandler handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Deletes a comment when requested by its author or the project owner.
    /// </summary>
    [HttpDelete("{commentId:guid}")]
    public async Task<IActionResult> DeleteComment(
        Guid projectId,
        Guid taskId,
        Guid commentId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<bool>.Error(401, "User not authenticated"));
        }

        var result = await _handler.HandleAsync(
            new DeleteProjectTaskCommentCommand(userId, projectId, taskId, commentId),
            cancellationToken);
        return ToActionResult(result, value => value);
    }
}
