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