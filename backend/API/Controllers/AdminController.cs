using Application.DTOs.Admin;
using Application.Interfaces;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;
        private readonly ILogger<AdminController> _logger;
        public AdminController(IAdminService adminService, ILogger<AdminController> logger)
        {
            _adminService = adminService;
            _logger = logger;
        }

        [HttpGet("dashboard-stats")]
        public async Task<ActionResult<ApiResponse<AdminDashboardStatsDto>>> GetDashboardStatsAsync()
        {
            var result = await _adminService.GetDashboardStatsAsync();
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("users")]
        public async Task<ActionResult<ApiResponse<List<AdminUserListItemDto>>>> GetUsersAsync([FromQuery] AdminUserFilterRequestDto request)
        {
            var result = await _adminService.GetUsersAsync(request);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("users/{userId:guid}")]
        public async Task<ActionResult<ApiResponse<AdminUserDetailsDto>>> GetUserDetailsByIdAsync([FromRoute] Guid userId)
        {
            var result = await _adminService.GetUserDetailsByIdAsync(userId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("users/by-email")]
        public async Task<ActionResult<ApiResponse<AdminUserDetailsDto>>> GetUserDetailsByEmailAsync([FromQuery] string email)
        {
            var result = await _adminService.GetUserDetailsByEmailAsync(email);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("users/{userId:guid}")]
        public async Task<ActionResult<ApiResponse<AdminUserDetailsDto>>> UpdateUserAsync([FromRoute] Guid userId, [FromBody] AdminUpdateUserRequestDto dto)
        {
            var result = await _adminService.UpdateUserAsync(userId, dto);

            if (result.StatusCode == 404)
            {
                _logger.LogWarning("Admin tried to update missing user {UserId}", userId);
            }

            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("users/{userId:guid}/role")]
        public async Task<ActionResult<ApiResponse<AdminUserDetailsDto>>> UpdateUserRoleAsync([FromRoute] Guid userId, [FromBody] UserRole newRole)
        {
            var result = await _adminService.UpdateUserRoleAsync(userId, newRole);

            if (result.StatusCode == 404)
            {
                _logger.LogWarning("Admin tried to change role for missing user {UserId}", userId);
            }

            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("users/{userId:guid}/activate")]
        public async Task<ActionResult<ApiResponse<AdminUserDetailsDto>>> ActivateUserAsync([FromRoute] Guid userId)
        {
            var result = await _adminService.ActivateUserAsync(userId);

            if (result.StatusCode == 404)
            {
                _logger.LogWarning("Admin tried to activate missing user {UserId}", userId);
            }

            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("users/{userId:guid}/deactivate")]
        public async Task<ActionResult<ApiResponse<AdminUserDetailsDto>>> DeactivateUserAsync([FromRoute] Guid userId)
        {
            var result = await _adminService.DeactivateUserAsync(userId);
            
            if (result.StatusCode == 404)
            {
                _logger.LogWarning("Admin tried to deactivate missing user {UserId}", userId);
            }

            return StatusCode(result.StatusCode, result);
        }

        [HttpDelete("users/{userId:guid}")]
        public async Task<ActionResult<ApiResponse<AdminUserDetailsDto>>> DeleteUserAsync([FromRoute] Guid userId)
        {
            var result = await _adminService.DeleteUserAsync(userId);

            if (result.StatusCode == 404)
            {
                _logger.LogWarning("Admin tried to delete missing user {UserId}", userId);
            }

            return StatusCode(result.StatusCode, result);
        }
    }
}
