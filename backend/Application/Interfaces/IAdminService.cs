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
        Task<ApiResponse<AdminDashboardStatsDto>> GetDashboardStatsAsync();
        Task<ApiResponse<List<AdminUserListItemDto>>> GetUsersAsync(AdminUserFilterRequestDto adminUserGetRequestDto);
        Task<ApiResponse<AdminUserDetailsDto>> GetUserDetailsByIdAsync(Guid userId);
        Task<ApiResponse<AdminUserDetailsDto>> GetUserDetailsByEmailAsync(string email);

        Task<ApiResponse<AdminUserDetailsDto>> UpdateUserAsync(Guid userId, AdminUpdateUserRequestDto dto);
        Task<ApiResponse<AdminUserDetailsDto>> UpdateUserRoleAsync(Guid userId, UserRole newRole);
        Task<ApiResponse<AdminUserDetailsDto>> ActivateUserAsync(Guid userId);
        Task<ApiResponse<AdminUserDetailsDto>> DeactivateUserAsync(Guid userId);
        Task<ApiResponse<AdminUserDetailsDto>> DeleteUserAsync(Guid userId);


    }
}
