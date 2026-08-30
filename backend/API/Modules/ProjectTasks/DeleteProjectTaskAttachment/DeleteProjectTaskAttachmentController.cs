using API.Modules.ProjectTasks;
using Application.Modules.ProjectTasks.DeleteProjectTaskAttachment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;

namespace API.Modules.ProjectTasks.DeleteProjectTaskAttachment;

/// <summary>
/// HTTP adapter for the delete-project-task-attachment vertical slice.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/tasks/{taskId:guid}/attachments")]
[Authorize]
public sealed class DeleteProjectTaskAttachmentController : ProjectTaskControllerBase
{
    private readonly IDeleteProjectTaskAttachmentHandler _handler;

    public DeleteProjectTaskAttachmentController(IDeleteProjectTaskAttachmentHandler handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Deletes an attachment when requested by its uploader or the project owner.
    /// </summary>
    [HttpDelete("{attachmentId:guid}")]
    public async Task<IActionResult> DeleteAttachment(
        Guid projectId,
        Guid taskId,
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<bool>.Error(401, "User not authenticated"));
        }

        var result = await _handler.HandleAsync(
            new DeleteProjectTaskAttachmentCommand(
                userId,
                projectId,
                taskId,
                attachmentId),
            cancellationToken);
        return ToActionResult(result, value => value);
    }
}
