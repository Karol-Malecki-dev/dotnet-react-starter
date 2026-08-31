using API.Contracts.Projects;
using API.Modules.Projects;
using Application.Modules.Projects.CreateProject;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;

namespace API.Modules.Projects.CreateProject;

/// <summary>
/// HTTP adapter for the create-project vertical slice.
/// </summary>
[ApiController]
[Route("api/projects")]
[Authorize]
public sealed class CreateProjectController : ProjectControllerBase
{
    private readonly ICreateProjectHandler _handler;

    public CreateProjectController(ICreateProjectHandler handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Creates a project owned by the current user.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateProject(
        CreateProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<ProjectResponse>.Error(401, "User not authenticated"));
        }

        var result = await _handler.HandleAsync(
            new CreateProjectCommand(ownerId, request.Name, request.Description),
            cancellationToken);

        return ToActionResult(result, MapProject);
    }
}
