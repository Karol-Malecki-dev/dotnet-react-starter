using API.Contracts.Projects;
using Application.Features.Projects;
using Application.Features.ProjectManagement.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace API.Controllers;

/// <summary>
/// Provides discussion endpoints for individual project tasks.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/tasks/{taskId:guid}/comments")]
[Authorize]
public class ProjectTaskCommentsController : ControllerBase
{
    private readonly IProjectTaskCommentApplicationService _commentService;

    public ProjectTaskCommentsController(IProjectTaskCommentApplicationService commentService)
    {
        _commentService = commentService;
    }

    /// <summary>Returns comments in chronological order for a project task.</summary>
    [HttpGet]
    public async Task<IActionResult> GetComments(Guid projectId, Guid taskId, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<IReadOnlyList<ProjectTaskCommentResponse>>.Error(401, "User not authenticated"));
        }

        var result = await _commentService.GetProjectTaskCommentsAsync(userId, projectId, taskId, cancellationToken);
        return ToActionResult(result, comments => comments.Select(MapComment).ToList());
    }

    /// <summary>Adds a comment to a project task for an eligible project member.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateComment(Guid projectId, Guid taskId, CreateProjectTaskCommentRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<ProjectTaskCommentResponse>.Error(401, "User not authenticated"));
        }

        var result = await _commentService.CreateProjectTaskCommentAsync(
            new CreateProjectTaskCommentCommand(userId, projectId, taskId, request.Content), cancellationToken);
        return ToActionResult(result, MapComment);
    }

    /// <summary>Deletes a comment when requested by its author or the project owner.</summary>
    [HttpDelete("{commentId:guid}")]
    public async Task<IActionResult> DeleteComment(Guid projectId, Guid taskId, Guid commentId, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<bool>.Error(401, "User not authenticated"));
        }

        var result = await _commentService.DeleteProjectTaskCommentAsync(userId, projectId, taskId, commentId, cancellationToken);
        return ToActionResult(result, value => value);
    }

    private IActionResult ToActionResult<TValue, TResponse>(ProjectOperationResult<TValue> result, Func<TValue, TResponse> map)
    {
        if (!result.IsSuccess)
        {
            var statusCode = result.Status switch
            {
                ProjectOperationStatus.NotFound => 404,
                ProjectOperationStatus.ValidationError => 400,
                ProjectOperationStatus.Forbidden => 403,
                _ => 500
            };
            return StatusCode(statusCode, ApiResponse<TResponse>.Error(statusCode, result.Message));
        }

        return StatusCode(result.CreatedStatusCode,
            ApiResponse<TResponse>.Success(map(result.Value!), result.Message, result.CreatedStatusCode));
    }

    private static ProjectTaskCommentResponse MapComment(ProjectTaskCommentView comment) => new(
        comment.Id,
        comment.ProjectTaskId,
        comment.AuthorUserId,
        comment.AuthorDisplayName,
        comment.Content,
        comment.CreatedAt);

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var userIdValue = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdValue, out userId);
    }
}