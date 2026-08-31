using API.Contracts.Projects;
using API.Modules.ProjectTasks;
using Application.Modules.ProjectTasks.ListProjectTaskAttachments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;

namespace API.Modules.ProjectTasks.ListProjectTaskAttachments;

/// <summary>
/// HTTP adapter for the list-project-task-attachments vertical slice.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/tasks/{taskId:guid}/attachments")]
[Authorize]
public sealed class ListProjectTaskAttachmentsController : ProjectTaskControllerBase
{
    private readonly IListProjectTaskAttachmentsHandler _handler;

    public ListProjectTaskAttachmentsController(IListProjectTaskAttachmentsHandler handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Returns attachment metadata in reverse chronological order.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListAttachments(
        Guid projectId,
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<IReadOnlyList<ProjectTaskAttachmentResponse>>.Error(
                401,
                "User not authenticated"));
        }

        var result = await _handler.HandleAsync(
            new ListProjectTaskAttachmentsQuery(userId, projectId, taskId),
            cancellationToken);
        return ToActionResult(
            result,
            attachments => attachments.Select(MapAttachment).ToList());
    }
}
