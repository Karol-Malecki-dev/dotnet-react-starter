using Application.DTOs.Project;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace API.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/tasks")]
[Authorize]
public class ProjectTasksController : ControllerBase
{
    private readonly IProjectTaskService _projectTaskService;

    public ProjectTasksController(IProjectTaskService projectTaskService)
    {
        _projectTaskService = projectTaskService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<ProjectTaskDto>>>> GetTasks(Guid projectId, [FromQuery] ProjectTaskQueryDto query)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<PagedResult<ProjectTaskDto>>.Error(401, "User not authenticated"));
        }

        var result = await _projectTaskService.GetProjectTasksAsync(ownerId, projectId, query);
        return StatusCode(result.StatusCode, result);
    }

    [HttpGet("{taskId:guid}")]
    public async Task<ActionResult<ApiResponse<ProjectTaskDto>>> GetTask(Guid projectId, Guid taskId)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<ProjectTaskDto>.Error(401, "User not authenticated"));
        }

        var result = await _projectTaskService.GetProjectTaskAsync(ownerId, projectId, taskId);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ProjectTaskDto>>> CreateTask(Guid projectId, CreateProjectTaskDto dto)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<ProjectTaskDto>.Error(401, "User not authenticated"));
        }

        var result = await _projectTaskService.CreateProjectTaskAsync(ownerId, projectId, dto);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPut("{taskId:guid}")]
    public async Task<ActionResult<ApiResponse<ProjectTaskDto>>> UpdateTask(Guid projectId, Guid taskId, UpdateProjectTaskDto dto)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<ProjectTaskDto>.Error(401, "User not authenticated"));
        }

        var result = await _projectTaskService.UpdateProjectTaskAsync(ownerId, projectId, taskId, dto);
        return StatusCode(result.StatusCode, result);
    }

    [HttpPatch("{taskId:guid}/status")]
    public async Task<ActionResult<ApiResponse<ProjectTaskDto>>> UpdateTaskStatus(Guid projectId, Guid taskId, UpdateProjectTaskStatusDto dto)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<ProjectTaskDto>.Error(401, "User not authenticated"));
        }

        var result = await _projectTaskService.UpdateProjectTaskStatusAsync(ownerId, projectId, taskId, dto);
        return StatusCode(result.StatusCode, result);
    }

    [HttpDelete("{taskId:guid}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteTask(Guid projectId, Guid taskId)
    {
        if (!TryGetCurrentUserId(out var ownerId))
        {
            return Unauthorized(ApiResponse<bool>.Error(401, "User not authenticated"));
        }

        var result = await _projectTaskService.DeleteProjectTaskAsync(ownerId, projectId, taskId);
        return StatusCode(result.StatusCode, result);
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var userIdValue = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdValue, out userId);
    }
}