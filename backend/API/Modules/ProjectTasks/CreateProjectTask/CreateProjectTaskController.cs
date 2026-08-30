using API.Contracts.Projects;
using API.Modules.ProjectTasks;
using Application.Modules.ProjectTasks.CreateProjectTask;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;

namespace API.Modules.ProjectTasks.CreateProjectTask;

/// <summary>
/// HTTP adapter for the create-project-task vertical slice.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}/tasks")]
[Authorize]
public sealed class CreateProjectTaskController : ProjectTaskControllerBase
{
    private readonly ICreateProjectTaskHandler _handler;

    public CreateProjectTaskController(ICreateProjectTaskHandler handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Creates a task for a project when the current user can mutate that project.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateTask(
        Guid projectId,
        CreateProjectTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<ProjectTaskResponse>.Error(401, "User not authenticated"));
        }

        var result = await _handler.HandleAsync(
            new CreateProjectTaskCommand(
                ownerId,
                projectId,
                request.Title,
                request.Description,
                request.Priority,
                request.DueDate,
                request.AssignedUserId,
                request.Labels ?? []),
            cancellationToken);

        return ToActionResult(result, MapTask);
    }
}
