using Application.DTOs.Auth;
using Application.DTOs.User;
using Application.Interfaces;
using Domain.Entities;
using Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Org.BouncyCastle.Asn1.Cmp;
using Shared.Responses;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UsersController> _logger;


    public UsersController(IUserService userService, ILogger<UsersController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<UserDto>>> GetUserById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _userService.GetUserByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<List<UserDto>>>> GetAllUsers([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var result = await _userService.GetAllUsersPagedAsync(pageNumber, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<UserDto>>> GetMe(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<UserDto>.Error(401, "User not authenticated"));
        }

        var result = await _userService.GetUserByIdAsync(userId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("count")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<int>>> GetCount(CancellationToken cancellationToken)
    {
        var result = await _userService.GetUserCountAsync(cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _userService.DeleteUserAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{id:guid}/display-name")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<UserDto>>> UpdateDisplayName(Guid id, [FromBody] string displayName, CancellationToken cancellationToken)
    {
        var result = await _userService.UpdateDisplayNameAsync(id, displayName, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{id:guid}/role")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<UserDto>>> UpdateRole(Guid id, [FromBody] string role, CancellationToken cancellationToken)
    {
        var result = await _userService.UpdateUserRoleAsync(id, role, cancellationToken);
        return Ok(result);
    }

    [HttpPut("me")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<UserDto>>> UpdateMe([FromBody] UpdateUserDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<UserDto>.Error(401, "User not authenticated"));
        }

        var result = await _userService.UpdateUserAsync(userId, dto, cancellationToken);
        return Ok(result);
    }
    private bool TryGetCurrentUserId(out Guid userId)
    {
        var userIdValue = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdValue, out userId);
    }

    [HttpGet("me/security")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<UserSecurityDto>>> GetUserSecurity(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<UserSecurityDto>.Error(401, "User not authenticated",null));
        }
        try
        {
            var result = await _userService.GetUserSecurityAsync(userId, cancellationToken);
            return StatusCode(result.StatusCode, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Two-factor authentication update error for user {UserId}", userId);
            return StatusCode(500, ApiResponse<UserSecurityDto>.Error(500, "Internal server error", null));
        }
    }

    [HttpPatch("me/security/two-factor")]
    [Authorize]
    public async Task<IActionResult> UpdateEmailTwoFactor([FromBody] UpdateTwoFactorPreferenceDto enable, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized(ApiResponse<UserSecurityDto>.Error(401, "User not authenticated", null));
        }
        try
        {
            var result = await _userService.UpdateTwoFactorAsync(userId, enable, cancellationToken);
            return StatusCode(result.StatusCode, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Two-factor authentication update error for user {UserId}", userId);
            return StatusCode(500, ApiResponse<UserSecurityDto>.Error(500, "Internal server error", null));
        }
    }
}
