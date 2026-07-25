using Application.DTOs.Project;
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
    private readonly IProjectService _projectService;

    public ProjectsController(IProjectService projectService)
    {
        _projectService = projectService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ProjectDto>>>> GetProjects([FromQuery] bool includeArchived = false, [FromQuery] string scope = "all")
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<List<ProjectDto>>.Error(401, "User not authenticated"));
        }

        var result = await _projectService.GetUserProjectsAsync(ownerId, includeArchived, scope);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("{projectId:guid}")]
    public async Task<ActionResult<ApiResponse<ProjectDto>>> GetProject(
        Guid projectId,
        [FromQuery] bool includeArchived = false)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<ProjectDto>.Error(401, "User not authenticated"));
        }

        var result = await _projectService.GetProjectAsync(ownerId, projectId, includeArchived);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ProjectDto>>> CreateProject(CreateProjectDto dto)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<ProjectDto>.Error(401, "User not authenticated"));
        }

        var result = await _projectService.CreateProjectAsync(ownerId, dto);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPut("{projectId:guid}")]
    public async Task<ActionResult<ApiResponse<ProjectDto>>> UpdateProject(Guid projectId, UpdateProjectDto dto)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<ProjectDto>.Error(401, "User not authenticated"));
        }

        var result = await _projectService.UpdateProjectAsync(ownerId, projectId, dto);
        return StatusCode(result.StatusCode, result);
    }

    [HttpDelete("{projectId:guid}")]
    public async Task<ActionResult<ApiResponse<bool>>> ArchiveProject(Guid projectId)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<bool>.Error(401, "User not authenticated"));
        }

        var result = await _projectService.ArchiveProjectAsync(ownerId, projectId);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("{projectId:guid}/members")]
    public async Task<ActionResult<ApiResponse<List<ProjectMemberDto>>>> GetMembers(Guid projectId)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<List<ProjectMemberDto>>.Error(401, "User not authenticated"));
        }

        var result = await _projectService.GetProjectMembersAsync(ownerId, projectId);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("{projectId:guid}/members/available")]
    public async Task<ActionResult<ApiResponse<List<ProjectMemberUserDto>>>> GetAvailableMembers(Guid projectId)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<List<ProjectMemberUserDto>>.Error(401, "User not authenticated"));
        }

        var result = await _projectService.GetAvailableProjectMembersAsync(ownerId, projectId);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost("{projectId:guid}/members")]
    public async Task<ActionResult<ApiResponse<ProjectMemberDto>>> AddMember(Guid projectId, AddProjectMemberDto dto)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<ProjectMemberDto>.Error(401, "User not authenticated"));
        }

        var result = await _projectService.AddProjectMemberAsync(ownerId, projectId, dto.UserId);
        return StatusCode(result.StatusCode, result);
    }

    [HttpDelete("{projectId:guid}/members/{userId:guid}")]
    public async Task<ActionResult<ApiResponse<bool>>> RemoveMember(Guid projectId, Guid userId)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<bool>.Error(401, "User not authenticated"));
        }

        var result = await _projectService.RemoveProjectMemberAsync(ownerId, projectId, userId);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPatch("{projectId:guid}/members/{userId:guid}/role")]
    public async Task<ActionResult<ApiResponse<ProjectMemberDto>>> UpdateMemberRole(Guid projectId, Guid userId, UpdateProjectMemberRoleDto dto)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<ProjectMemberDto>.Error(401, "User not authenticated"));
        }

        var result = await _projectService.UpdateProjectMemberRoleAsync(ownerId, projectId, userId, dto.Role);
        return StatusCode(result.StatusCode, result);
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var userIdValue = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdValue, out userId);
    }
}