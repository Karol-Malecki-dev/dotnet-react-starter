using API.Contracts.Projects;
using API.Modules.Projects;
using Application.Modules.Projects.UpdateProject;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;

namespace API.Modules.Projects.UpdateProject;

/// <summary>
/// HTTP adapter for the update-project vertical slice.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}")]
[Authorize]
public sealed class UpdateProjectController : ProjectControllerBase
{
    private readonly IUpdateProjectHandler _handler;

    public UpdateProjectController(IUpdateProjectHandler handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Updates a project owned by the current user.
    /// </summary>
    [HttpPut]
    public async Task<IActionResult> UpdateProject(
        Guid projectId,
        UpdateProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<ProjectResponse>.Error(401, "User not authenticated"));
        }

        var result = await _handler.HandleAsync(
            new UpdateProjectCommand(
                ownerId,
                projectId,
                request.Name,
                request.Description,
                request.ConcurrencyStamp),
            cancellationToken);

        return ToActionResult(result, MapProject);
    }
}
