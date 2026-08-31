using Application.DTOs.Admin;
using Application.Interfaces;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Responses;
using System.IdentityModel.Tokens.Jwt;

namespace API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;
        private readonly ILogger<AdminController> _logger;
        private readonly IAccountSecurityAuditWriter? _auditWriter;
        public AdminController(
            IAdminService adminService,
            ILogger<AdminController> logger,
            IAccountSecurityAuditWriter? auditWriter = null)
        {
            _adminService = adminService;
            _logger = logger;
            _auditWriter = auditWriter;
        }

        [HttpGet("dashboard-stats")]
        public async Task<ActionResult<ApiResponse<AdminDashboardStatsDto>>> GetDashboardStatsAsync(CancellationToken cancellationToken)
        {
            var result = await _adminService.GetDashboardStatsAsync(cancellationToken);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("users")]
        public async Task<ActionResult<ApiResponse<List<AdminUserListItemDto>>>> GetUsersAsync([FromQuery] AdminUserFilterRequestDto request, CancellationToken cancellationToken)
        {
            var result = await _adminService.GetUsersAsync(request, cancellationToken);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Returns a bounded page of account security events for administrators.
        /// </summary>
        [HttpGet("security-events")]
        public async Task<ActionResult<ApiResponse<AdminPagedResultDto<AdminAccountSecurityEventDto>>>> GetAccountSecurityEventsAsync(
            [FromQuery] AdminAccountSecurityEventFilterRequestDto request,
            CancellationToken cancellationToken)
        {
            var result = await _adminService.GetAccountSecurityEventsAsync(request, cancellationToken);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("users/{userId:guid}")]
        public async Task<ActionResult<ApiResponse<AdminUserDetailsDto>>> GetUserDetailsByIdAsync([FromRoute] Guid userId, CancellationToken cancellationToken)
        {
            var result = await _adminService.GetUserDetailsByIdAsync(userId, cancellationToken);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("users/by-email")]
        public async Task<ActionResult<ApiResponse<AdminUserDetailsDto>>> GetUserDetailsByEmailAsync([FromQuery] string email, CancellationToken cancellationToken)
        {
            var result = await _adminService.GetUserDetailsByEmailAsync(email, cancellationToken);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("users/{userId:guid}")]
        public async Task<ActionResult<ApiResponse<AdminUserDetailsDto>>> UpdateUserAsync([FromRoute] Guid userId, [FromBody] AdminUpdateUserRequestDto dto, CancellationToken cancellationToken)
        {
            var result = await _adminService.UpdateUserAsync(userId, dto, cancellationToken);

            if (result.StatusCode == 404)
            {
                _logger.LogWarning("Admin tried to update missing user {UserId}", userId);
            }

            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("users/{userId:guid}/role")]
        public async Task<ActionResult<ApiResponse<AdminUserDetailsDto>>> UpdateUserRoleAsync([FromRoute] Guid userId, [FromBody] UserRole newRole, CancellationToken cancellationToken)
        {
            var result = await _adminService.UpdateUserRoleAsync(userId, newRole, cancellationToken);

            if (result.StatusCode is >= 200 and < 300)
            {
                await WriteSecurityAuditAsync("account.role.changed", userId, newRole.ToString(), cancellationToken);
            }

            if (result.StatusCode == 404)
            {
                _logger.LogWarning("Admin tried to change role for missing user {UserId}", userId);
            }

            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("users/{userId:guid}/activate")]
        public async Task<ActionResult<ApiResponse<AdminUserDetailsDto>>> ActivateUserAsync([FromRoute] Guid userId, CancellationToken cancellationToken)
        {
            var result = await _adminService.ActivateUserAsync(userId, cancellationToken);

            if (result.StatusCode is >= 200 and < 300)
            {
                await WriteSecurityAuditAsync("account.status.changed", userId, "active", cancellationToken);
            }

            if (result.StatusCode == 404)
            {
                _logger.LogWarning("Admin tried to activate missing user {UserId}", userId);
            }

            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("users/{userId:guid}/deactivate")]
        public async Task<ActionResult<ApiResponse<AdminUserDetailsDto>>> DeactivateUserAsync([FromRoute] Guid userId, CancellationToken cancellationToken)
        {
            var result = await _adminService.DeactivateUserAsync(userId, cancellationToken);

            if (result.StatusCode is >= 200 and < 300)
            {
                await WriteSecurityAuditAsync("account.status.changed", userId, "inactive", cancellationToken);
            }
            
            if (result.StatusCode == 404)
            {
                _logger.LogWarning("Admin tried to deactivate missing user {UserId}", userId);
            }

            return StatusCode(result.StatusCode, result);
        }

        private Task WriteSecurityAuditAsync(string eventCode, Guid subjectUserId, string reason, CancellationToken cancellationToken)
        {
            if (_auditWriter is null)
            {
                return Task.CompletedTask;
            }

            var actorValue = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var actorUserId = Guid.TryParse(actorValue, out var parsedActorId) ? parsedActorId : (Guid?)null;

            return _auditWriter.WriteAsync(new AccountSecurityAuditEntry(
                eventCode,
                "success",
                ActorUserId: actorUserId,
                SubjectUserId: subjectUserId,
                CorrelationId: HttpContext.TraceIdentifier,
                Metadata: new Dictionary<string, string> { ["reason"] = reason }),
                cancellationToken);
        }

        [HttpDelete("users/{userId:guid}")]
        public async Task<ActionResult<ApiResponse<AdminUserDetailsDto>>> DeleteUserAsync([FromRoute] Guid userId, CancellationToken cancellationToken)
        {
            var result = await _adminService.DeleteUserAsync(userId, cancellationToken);

            if (result.StatusCode == 404)
            {
                _logger.LogWarning("Admin tried to delete missing user {UserId}", userId);
            }

            return StatusCode(result.StatusCode, result);
        }
    }
}
