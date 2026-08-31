using Application.DTOs.Admin;
using Application.DTOs.User;
using Domain.Enums;
using Shared.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces
{
    public interface IAdminService
    {
        Task<ApiResponse<AdminDashboardStatsDto>> GetDashboardStatsAsync(CancellationToken cancellationToken = default);
        Task<ApiResponse<List<AdminUserListItemDto>>> GetUsersAsync(AdminUserFilterRequestDto adminUserGetRequestDto, CancellationToken cancellationToken = default);
        Task<ApiResponse<AdminPagedResultDto<AdminAccountSecurityEventDto>>> GetAccountSecurityEventsAsync(
            AdminAccountSecurityEventFilterRequestDto request,
            CancellationToken cancellationToken = default);
        Task<ApiResponse<AdminUserDetailsDto>> GetUserDetailsByIdAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<ApiResponse<AdminUserDetailsDto>> GetUserDetailsByEmailAsync(string email, CancellationToken cancellationToken = default);

        Task<ApiResponse<AdminUserDetailsDto>> UpdateUserAsync(Guid userId, AdminUpdateUserRequestDto dto, CancellationToken cancellationToken = default);
        Task<ApiResponse<AdminUserDetailsDto>> UpdateUserRoleAsync(Guid userId, UserRole newRole, CancellationToken cancellationToken = default);
        Task<ApiResponse<AdminUserDetailsDto>> ActivateUserAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<ApiResponse<AdminUserDetailsDto>> DeactivateUserAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<ApiResponse<AdminUserDetailsDto>> DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default);


    }
}
