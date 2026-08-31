using API.Contracts.Projects;
using API.Modules.ProjectTasks;
using Application.Modules.ProjectTasks.CreateProjectTaskComment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;

namespace API.Modules.ProjectTasks.CreateProjectTaskComment;

/// <summary>
/// HTTP adapter for the create-project-task-comment vertical slice.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/tasks/{taskId:guid}/comments")]
[Authorize]
public sealed class CreateProjectTaskCommentController : ProjectTaskControllerBase
{
    private readonly ICreateProjectTaskCommentHandler _handler;

    public CreateProjectTaskCommentController(ICreateProjectTaskCommentHandler handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Adds a comment to an accessible project task for a non-viewer member.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateComment(
        Guid projectId,
        Guid taskId,
        CreateProjectTaskCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<ProjectTaskCommentResponse>.Error(
                401,
                "User not authenticated"));
        }

        var result = await _handler.HandleAsync(
            new CreateProjectTaskCommentCommand(userId, projectId, taskId, request.Content),
            cancellationToken);
        return ToActionResult(result, MapComment);
    }
}
