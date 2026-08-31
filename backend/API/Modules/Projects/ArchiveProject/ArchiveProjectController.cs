using API.Modules.Projects;
using Application.Features.Projects;
using Application.Modules.Projects.ArchiveProject;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;

namespace API.Modules.Projects.ArchiveProject;

/// <summary>
/// HTTP adapter for the archive-project vertical slice.
/// </summary>
[ApiController]
[Route("api/projects/{projectId:guid}")]
[Authorize]
public sealed class ArchiveProjectController : ProjectControllerBase
{
    private readonly IArchiveProjectHandler _handler;

    public ArchiveProjectController(IArchiveProjectHandler handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// Archives a project owned by the current user.
    /// </summary>
    [HttpDelete]
    public async Task<IActionResult> ArchiveProject(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<bool>.Error(401, "User not authenticated"));
        }

        var result = await _handler.HandleAsync(
            new ArchiveProjectCommand(ownerId, projectId),
            cancellationToken);

        return ToActionResult(result, value => value);
    }
}
