using API.Contracts.Projects;
using API.Modules.Projects;
using Application.Features.Projects;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;

namespace API.Controllers;

[ApiController]
[Route("api/projects")]
[Authorize]
public class ProjectsController : ProjectControllerBase
{
    private readonly IProjectManagementService _projectService;
    private readonly IProjectMembershipApplicationService _membershipService;

    public ProjectsController(
        IProjectManagementService projectService,
        IProjectMembershipApplicationService membershipService)
    {
        _projectService = projectService;
        _membershipService = membershipService;
    }

    [HttpGet]
    public async Task<IActionResult> GetProjects([FromQuery] bool includeArchived = false, [FromQuery] string scope = "all", CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<List<ProjectResponse>>.Error(401, "User not authenticated"));
        }

        var result = await _projectService.GetUserProjectsAsync(ownerId, includeArchived, scope, cancellationToken);
        return ToActionResult(result, projects => projects.Select(MapProject).ToList());
    }

    [HttpPost]
    public async Task<IActionResult> CreateProject(CreateProjectRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<ProjectResponse>.Error(401, "User not authenticated"));
        }

        var result = await _projectService.CreateProjectAsync(new CreateProjectCommand(ownerId, request.Name, request.Description), cancellationToken);
        return ToActionResult(result, MapProject);
    }

    [HttpPut("{projectId:guid}")]
    public async Task<IActionResult> UpdateProject(Guid projectId, UpdateProjectRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<ProjectResponse>.Error(401, "User not authenticated"));
        }

        var result = await _projectService.UpdateProjectAsync(new UpdateProjectCommand(
            ownerId,
            projectId,
            request.Name,
            request.Description,
            request.ConcurrencyStamp), cancellationToken);
        return ToActionResult(result, MapProject);
    }

    [HttpDelete("{projectId:guid}")]
    public async Task<IActionResult> ArchiveProject(Guid projectId, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<bool>.Error(401, "User not authenticated"));
        }

        var result = await _projectService.ArchiveProjectAsync(ownerId, projectId, cancellationToken);
        return ToActionResult(result, value => value);
    }

    [HttpGet("{projectId:guid}/members")]
    public async Task<IActionResult> GetMembers(Guid projectId, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<List<ProjectMemberResponse>>.Error(401, "User not authenticated"));
        }

        var result = await _membershipService.GetProjectMembersAsync(ownerId, projectId, cancellationToken);
        return ToActionResult(result, members => members.Select(MapMember).ToList());
    }

    [HttpGet("{projectId:guid}/activity")]
    public async Task<IActionResult> GetActivity(Guid projectId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<PagedProjectActivityView>.Error(401, "User not authenticated"));
        }

        var result = await _projectService.GetProjectActivitiesAsync(userId, projectId, pageNumber, pageSize, cancellationToken);
        return ToActionResult(result, value => value);
    }

    [HttpGet("{projectId:guid}/dashboard")]
    public async Task<IActionResult> GetDashboard(Guid projectId, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<ProjectDashboardView>.Error(401, "User not authenticated"));
        }

        var result = await _projectService.GetProjectDashboardAsync(userId, projectId, cancellationToken);
        return ToActionResult(result, value => value);
    }

    [HttpGet("{projectId:guid}/members/available")]
    public async Task<IActionResult> GetAvailableMembers(Guid projectId, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<List<ProjectMemberUserResponse>>.Error(401, "User not authenticated"));
        }

        var result = await _membershipService.GetAvailableProjectMembersAsync(ownerId, projectId, cancellationToken);
        return ToActionResult(result, users => users.Select(MapMemberUser).ToList());
    }

    [HttpPost("{projectId:guid}/members")]
    public async Task<IActionResult> AddMember(Guid projectId, AddProjectMemberRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<ProjectMemberResponse>.Error(401, "User not authenticated"));
        }

        var result = await _membershipService.AddProjectMemberAsync(ownerId, projectId, request.UserId, cancellationToken);
        return ToActionResult(result, MapMember);
    }

    [HttpDelete("{projectId:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid projectId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<bool>.Error(401, "User not authenticated"));
        }

        var result = await _membershipService.RemoveProjectMemberAsync(ownerId, projectId, userId, cancellationToken);
        return ToActionResult(result, value => value);
    }

    [HttpPatch("{projectId:guid}/members/{userId:guid}/role")]
    public async Task<IActionResult> UpdateMemberRole(Guid projectId, Guid userId, UpdateProjectMemberRoleRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<ProjectMemberResponse>.Error(401, "User not authenticated"));
        }

        var result = await _membershipService.UpdateProjectMemberRoleAsync(ownerId, projectId, userId, request.Role, cancellationToken);
        return ToActionResult(result, MapMember);
    }

}