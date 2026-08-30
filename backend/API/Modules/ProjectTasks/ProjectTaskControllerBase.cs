using API.Contracts.Projects;
using Application.Features.ProjectManagement.Tasks;
using Application.Features.Projects;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace API.Modules.ProjectTasks;

/// <summary>
/// Provides shared HTTP mapping for ProjectTasks controllers while endpoints remain slice-specific.
/// </summary>
public abstract class ProjectTaskControllerBase : ControllerBase
{
    protected IActionResult ToActionResult<TValue, TResponse>(
        ProjectOperationResult<TValue> result,
        Func<TValue, TResponse> map)
    {
        if (!result.IsSuccess)
        {
            var statusCode = MapStatusCode(result.Status);
            return StatusCode(statusCode, ApiResponse<TResponse>.Error(statusCode, result.Message));
        }

        return StatusCode(
            result.CreatedStatusCode,
            ApiResponse<TResponse>.Success(map(result.Value!), result.Message, result.CreatedStatusCode));
    }

    protected static ProjectTaskResponse MapTask(ProjectTaskView task) => new(
        task.Id,
        task.ProjectId,
        task.Title,
        task.Description,
        task.Status,
        task.Priority,
        task.DueDate,
        task.AssignedUserId,
        task.CreatedByUserId,
        task.CreatedAt,
        task.UpdatedAt,
        task.ConcurrencyStamp,
        task.Labels);

    protected static int MapStatusCode(ProjectOperationStatus status) => status switch
    {
        ProjectOperationStatus.NotFound => 404,
        ProjectOperationStatus.ValidationError => 400,
        ProjectOperationStatus.Conflict => 409,
        ProjectOperationStatus.Forbidden => 403,
        _ => 500
    };

    protected bool TryGetCurrentUserId(out Guid userId)
    {
        var userIdValue = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdValue, out userId);
    }
}
