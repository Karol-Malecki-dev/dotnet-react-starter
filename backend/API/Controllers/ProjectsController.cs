using API.Contracts.Projects;
using Application.Features.Projects;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace API.Controllers;

[ApiController]
[Route("api/projects")]
[Authorize]
public class ProjectsController : ControllerBase
{
    private readonly IProjectApplicationService _projectService;

    public ProjectsController(IProjectApplicationService projectService)
    {
        _projectService = projectService;
    }

    [HttpGet]
    public async Task<IActionResult> GetProjects([FromQuery] bool includeArchived = false, [FromQuery] string scope = "all")
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<List<ProjectResponse>>.Error(401, "User not authenticated"));
        }

        var result = await _projectService.GetUserProjectsAsync(ownerId, includeArchived, scope);
        return ToActionResult(result, projects => projects.Select(MapProject).ToList());
    }

    [HttpGet("{projectId:guid}")]
    public async Task<IActionResult> GetProject(
        Guid projectId,
        [FromQuery] bool includeArchived = false)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<ProjectResponse>.Error(401, "User not authenticated"));
        }

        var result = await _projectService.GetProjectAsync(ownerId, projectId, includeArchived);
        return ToActionResult(result, MapProject);
    }

    [HttpPost]
    public async Task<IActionResult> CreateProject(CreateProjectRequest request)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<ProjectResponse>.Error(401, "User not authenticated"));
        }

        var result = await _projectService.CreateProjectAsync(new CreateProjectCommand(ownerId, request.Name, request.Description));
        return ToActionResult(result, MapProject);
    }

    [HttpPut("{projectId:guid}")]
    public async Task<IActionResult> UpdateProject(Guid projectId, UpdateProjectRequest request)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<ProjectResponse>.Error(401, "User not authenticated"));
        }

        var result = await _projectService.UpdateProjectAsync(new UpdateProjectCommand(ownerId, projectId, request.Name, request.Description));
        return ToActionResult(result, MapProject);
    }

    [HttpDelete("{projectId:guid}")]
    public async Task<IActionResult> ArchiveProject(Guid projectId)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<bool>.Error(401, "User not authenticated"));
        }

        var result = await _projectService.ArchiveProjectAsync(ownerId, projectId);
        return ToActionResult(result, value => value);
    }

    [HttpGet("{projectId:guid}/members")]
    public async Task<IActionResult> GetMembers(Guid projectId)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<List<ProjectMemberResponse>>.Error(401, "User not authenticated"));
        }

        var result = await _projectService.GetProjectMembersAsync(ownerId, projectId);
        return ToActionResult(result, members => members.Select(MapMember).ToList());
    }

    [HttpGet("{projectId:guid}/activity")]
    public async Task<IActionResult> GetActivity(Guid projectId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<PagedProjectActivityView>.Error(401, "User not authenticated"));
        }

        var result = await _projectService.GetProjectActivitiesAsync(userId, projectId, pageNumber, pageSize);
        return ToActionResult(result, value => value);
    }

    [HttpGet("{projectId:guid}/members/available")]
    public async Task<IActionResult> GetAvailableMembers(Guid projectId)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<List<ProjectMemberUserResponse>>.Error(401, "User not authenticated"));
        }

        var result = await _projectService.GetAvailableProjectMembersAsync(ownerId, projectId);
        return ToActionResult(result, users => users.Select(MapMemberUser).ToList());
    }

    [HttpPost("{projectId:guid}/members")]
    public async Task<IActionResult> AddMember(Guid projectId, AddProjectMemberRequest request)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<ProjectMemberResponse>.Error(401, "User not authenticated"));
        }

        var result = await _projectService.AddProjectMemberAsync(ownerId, projectId, request.UserId);
        return ToActionResult(result, MapMember);
    }

    [HttpDelete("{projectId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid projectId, Guid userId)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<bool>.Error(401, "User not authenticated"));
        }

        var result = await _projectService.RemoveProjectMemberAsync(ownerId, projectId, userId);
        return ToActionResult(result, value => value);
    }

    [HttpPatch("{projectId:guid}/members/{userId:guid}/role")]
    public async Task<IActionResult> UpdateMemberRole(Guid projectId, Guid userId, UpdateProjectMemberRoleRequest request)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<ProjectMemberResponse>.Error(401, "User not authenticated"));
        }

        var result = await _projectService.UpdateProjectMemberRoleAsync(ownerId, projectId, userId, request.Role);
        return ToActionResult(result, MapMember);
    }

    private IActionResult ToActionResult<TValue, TResponse>(
        ProjectOperationResult<TValue> result,
        Func<TValue, TResponse> map)
    {
        if (!result.IsSuccess)
        {
            return StatusCode(MapStatusCode(result.Status), ApiResponse<TResponse>.Error(
                MapStatusCode(result.Status), result.Message));
        }

        return StatusCode(result.CreatedStatusCode,
            ApiResponse<TResponse>.Success(map(result.Value!), result.Message, result.CreatedStatusCode));
    }

    private static ProjectResponse MapProject(ProjectView project) => new(
        project.Id,
        project.Name,
        project.Description,
        project.OwnerId,
        project.CreatedAt,
        project.UpdatedAt,
        project.IsArchived,
        project.CurrentUserRole);

    private static ProjectMemberResponse MapMember(ProjectMemberView member) => new(
        member.UserId,
        member.DisplayName,
        member.Email,
        member.Role,
        member.AddedAt);

    private static ProjectMemberUserResponse MapMemberUser(ProjectMemberUserView user) => new(
        user.Id,
        user.DisplayName,
        user.Email);

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