using API.Contracts.Projects;
using API.Modules.ProjectTasks;
using Application.Modules.ProjectTasks.CreateProjectTaskAttachment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;

namespace API.Modules.ProjectTasks.CreateProjectTaskAttachment;

/// <summary>
/// HTTP adapter for the create-project-task-attachment vertical slice.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/tasks/{taskId:guid}/attachments")]
[Authorize]
public sealed class CreateProjectTaskAttachmentController : ProjectTaskControllerBase
{
    private readonly ICreateProjectTaskAttachmentHandler _handler;

    public CreateProjectTaskAttachmentController(ICreateProjectTaskAttachmentHandler handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Uploads an allowed file and associates it with a project task.
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<IActionResult> CreateAttachment(
        Guid projectId,
        Guid taskId,
        IFormFile? file,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<ProjectTaskAttachmentResponse>.Error(
                401,
                "User not authenticated"));
        }

        if (file is null)
        {
            return BadRequest(ApiResponse<ProjectTaskAttachmentResponse>.Error(
                400,
                "A file is required"));
        }

        await using var content = file.OpenReadStream();
        var result = await _handler.HandleAsync(
            new CreateProjectTaskAttachmentCommand(
                userId,
                projectId,
                taskId,
                file.FileName,
                file.ContentType,
                file.Length,
                content),
            cancellationToken);
        return ToActionResult(result, MapAttachment);
    }
}
