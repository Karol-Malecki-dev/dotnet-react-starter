using API.Contracts.Projects;
using API.Modules.ProjectTasks;
using Application.Modules.ProjectTasks.ListProjectTaskComments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;

namespace API.Modules.ProjectTasks.ListProjectTaskComments;

/// <summary>
/// HTTP adapter for the list-project-task-comments vertical slice.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/tasks/{taskId:guid}/comments")]
[Authorize]
public sealed class ListProjectTaskCommentsController : ProjectTaskControllerBase
{
    private readonly IListProjectTaskCommentsHandler _handler;

    public ListProjectTaskCommentsController(IListProjectTaskCommentsHandler handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Returns comments in chronological order for an accessible project task.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListComments(
        Guid projectId,
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<IReadOnlyList<ProjectTaskCommentResponse>>.Error(
                401,
                "User not authenticated"));
        }

        var result = await _handler.HandleAsync(
            new ListProjectTaskCommentsQuery(userId, projectId, taskId),
            cancellationToken);
        return ToActionResult(
            result,
            comments => comments.Select(MapComment).ToList());
    }
}
