using API.Contracts.Projects;
using API.Modules.ProjectTasks;
using Application.Modules.ProjectTasks.DownloadProjectTaskAttachment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;

namespace API.Modules.ProjectTasks.DownloadProjectTaskAttachment;

/// <summary>
/// HTTP adapter for the download-project-task-attachment vertical slice.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/tasks/{taskId:guid}/attachments")]
[Authorize]
public sealed class DownloadProjectTaskAttachmentController : ProjectTaskControllerBase
{
    private readonly IDownloadProjectTaskAttachmentHandler _handler;

    public DownloadProjectTaskAttachmentController(IDownloadProjectTaskAttachmentHandler handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Streams an attachment after verifying project and task access.
    /// </summary>
    [HttpGet("{attachmentId:guid}/download")]
    public async Task<IActionResult> DownloadAttachment(
        Guid projectId,
        Guid taskId,
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<ProjectTaskAttachmentResponse>.Error(
                401,
                "User not authenticated"));
        }

        var result = await _handler.HandleAsync(
            new DownloadProjectTaskAttachmentQuery(
                userId,
                projectId,
                taskId,
                attachmentId),
            cancellationToken);
        if (!result.IsSuccess)
        {
            var statusCode = MapStatusCode(result.Status);
            return StatusCode(
                statusCode,
                ApiResponse<ProjectTaskAttachmentResponse>.Error(statusCode, result.Message));
        }

        var download = result.Value!;
        return File(
            download.Content,
            download.ContentType,
            download.OriginalFileName,
            enableRangeProcessing: true);
    }
}
