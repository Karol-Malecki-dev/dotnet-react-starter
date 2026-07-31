using API.Contracts.Projects;
using Application.Features.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace API.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/tasks")]
[Authorize]
public class ProjectTasksController : ControllerBase
{
    private readonly IProjectTaskApplicationService _projectTaskService;
    private readonly IProjectTaskAttachmentApplicationService _attachmentService;

    public ProjectTasksController(
        IProjectTaskApplicationService projectTaskService,
        IProjectTaskAttachmentApplicationService attachmentService)
    {
        _projectTaskService = projectTaskService;
        _attachmentService = attachmentService;
    }

    [HttpGet]
    public async Task<IActionResult> GetTasks(Guid projectId, [FromQuery] ProjectTaskQueryRequest request)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<PagedProjectTaskResponse>.Error(401, "User not authenticated"));
        }

        var result = await _projectTaskService.GetProjectTasksAsync(new ProjectTaskQuery(
            ownerId,
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
            request.SortDirection));
        return ToActionResult(result, page => new PagedProjectTaskResponse(
            page.Items.Select(MapTask).ToList(), page.PageNumber, page.PageSize, page.TotalCount));
    }

    [HttpGet("{taskId:guid}")]
    public async Task<IActionResult> GetTask(Guid projectId, Guid taskId)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<ProjectTaskResponse>.Error(401, "User not authenticated"));
        }

        var result = await _projectTaskService.GetProjectTaskAsync(ownerId, projectId, taskId);
        return ToActionResult(result, MapTask);
    }

    [HttpPost]
    public async Task<IActionResult> CreateTask(Guid projectId, CreateProjectTaskRequest request)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<ProjectTaskResponse>.Error(401, "User not authenticated"));
        }

        var result = await _projectTaskService.CreateProjectTaskAsync(new CreateProjectTaskCommand(
            ownerId, projectId, request.Title, request.Description, request.Priority, request.DueDate, request.AssignedUserId, request.Labels ?? []));
        return ToActionResult(result, MapTask);
    }

    [HttpPut("{taskId:guid}")]
    public async Task<IActionResult> UpdateTask(Guid projectId, Guid taskId, UpdateProjectTaskRequest request)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<ProjectTaskResponse>.Error(401, "User not authenticated"));
        }

        var result = await _projectTaskService.UpdateProjectTaskAsync(new UpdateProjectTaskCommand(
            ownerId, projectId, taskId, request.Title, request.Description, request.Priority, request.DueDate, request.AssignedUserId, request.Labels ?? []));
        return ToActionResult(result, MapTask);
    }

    [HttpPatch("{taskId:guid}/status")]
    public async Task<IActionResult> UpdateTaskStatus(Guid projectId, Guid taskId, UpdateProjectTaskStatusRequest request)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<ProjectTaskResponse>.Error(401, "User not authenticated"));
        }

        var result = await _projectTaskService.UpdateProjectTaskStatusAsync(new UpdateProjectTaskStatusCommand(
            ownerId, projectId, taskId, request.Status));
        return ToActionResult(result, MapTask);
    }

    [HttpDelete("{taskId:guid}")]
    public async Task<IActionResult> DeleteTask(Guid projectId, Guid taskId)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<bool>.Error(401, "User not authenticated"));
        }

        var result = await _projectTaskService.DeleteProjectTaskAsync(ownerId, projectId, taskId);
        return ToActionResult(result, value => value);
    }

    [HttpGet("{taskId:guid}/attachments")]
    public async Task<IActionResult> GetAttachments(Guid projectId, Guid taskId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<IReadOnlyList<ProjectTaskAttachmentResponse>>.Error(401, "User not authenticated"));
        }

        var result = await _attachmentService.GetProjectTaskAttachmentsAsync(userId, projectId, taskId);
        return ToActionResult(result, attachments => attachments.Select(MapAttachment).ToList());
    }

    [HttpPost("{taskId:guid}/attachments")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<IActionResult> UploadAttachment(Guid projectId, Guid taskId, IFormFile? file)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<ProjectTaskAttachmentResponse>.Error(401, "User not authenticated"));
        }

        if (file is null)
        {
            return BadRequest(ApiResponse<ProjectTaskAttachmentResponse>.Error(400, "A file is required"));
        }

        await using var content = file.OpenReadStream();
        var result = await _attachmentService.CreateProjectTaskAttachmentAsync(new CreateProjectTaskAttachmentCommand(
            userId,
            projectId,
            taskId,
            file.FileName,
            file.ContentType,
            file.Length,
            content));
        return ToActionResult(result, MapAttachment);
    }

    [HttpGet("{taskId:guid}/attachments/{attachmentId:guid}/download")]
    public async Task<IActionResult> DownloadAttachment(Guid projectId, Guid taskId, Guid attachmentId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<ProjectTaskAttachmentResponse>.Error(401, "User not authenticated"));
        }

        var result = await _attachmentService.OpenProjectTaskAttachmentAsync(userId, projectId, taskId, attachmentId);
        if (!result.IsSuccess)
        {
            var statusCode = MapStatusCode(result.Status);
            return StatusCode(statusCode, ApiResponse<ProjectTaskAttachmentResponse>.Error(statusCode, result.Message));
        }

        var download = result.Value!;
        return File(download.Content, download.ContentType, download.OriginalFileName, enableRangeProcessing: true);
    }

    [HttpDelete("{taskId:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> DeleteAttachment(Guid projectId, Guid taskId, Guid attachmentId)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<bool>.Error(401, "User not authenticated"));
        }

        var result = await _attachmentService.DeleteProjectTaskAttachmentAsync(userId, projectId, taskId, attachmentId);
        return ToActionResult(result, value => value);
    }

    private IActionResult ToActionResult<TValue, TResponse>(
        ProjectOperationResult<TValue> result,
        Func<TValue, TResponse> map)
    {
        if (!result.IsSuccess)
        {
            var statusCode = MapStatusCode(result.Status);
            return StatusCode(statusCode, ApiResponse<TResponse>.Error(statusCode, result.Message));
        }

        return StatusCode(result.CreatedStatusCode,
            ApiResponse<TResponse>.Success(map(result.Value!), result.Message, result.CreatedStatusCode));
    }

    private static ProjectTaskResponse MapTask(ProjectTaskView task) => new(
        task.Id,
        task.ProjectId,
        task.Title,
        task.Description,
        task.Status,
        task.Priority,
        task.DueDate,
        task.AssignedUserId,
        task.CreatedByUserId,
        task.CreatedAt,
        task.UpdatedAt,
        task.Labels);

    private static ProjectTaskAttachmentResponse MapAttachment(ProjectTaskAttachmentView attachment) => new(
        attachment.Id,
        attachment.ProjectTaskId,
        attachment.UploadedByUserId,
        attachment.UploaderDisplayName,
        attachment.OriginalFileName,
        attachment.ContentType,
        attachment.SizeBytes,
        attachment.CreatedAt);

    private static int MapStatusCode(ProjectOperationStatus status) => status switch
    {
        ProjectOperationStatus.NotFound => 404,
        ProjectOperationStatus.ValidationError => 400,
        ProjectOperationStatus.Conflict => 409,
        ProjectOperationStatus.Forbidden => 403,
        _ => 500
    };

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var userIdValue = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdValue, out userId);
    }
}