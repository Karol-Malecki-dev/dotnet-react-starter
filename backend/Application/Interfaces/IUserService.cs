using Application.DTOs.Admin;
using Application.DTOs.User;
using Shared.Responses;

namespace Application.Interfaces;

public interface IUserService
{
    Task<ApiResponse<UserDto>> GetUserByIdAsync(Guid id);
    Task<ApiResponse<UserDto>> GetUserByEmailAsync(string email);
    Task<ApiResponse<List<UserDto>>> GetAllUsersPagedAsync(int pageNumber, int pageSize);
    Task<ApiResponse<int>> GetUserCountAsync();
    Task<ApiResponse<string>> GetUserRoleAsync(Guid userId);
    Task<ApiResponse<UserSecurityDto>> GetUserSecurityAsync(Guid userId);
    Task<ApiResponse<AdminDashboardStatsDto>> GetAdminDashboardStatsAsync(Guid userID);

    Task<ApiResponse<UserDto>> UpdateUserAsync(Guid userId, UpdateUserDto dto);
    Task<ApiResponse<UserDto>> UpdateDisplayNameAsync(Guid userId, string displayName);
    Task<ApiResponse<UserDto>> UpdateUserRoleAsync(Guid userId, string role);
    Task<ApiResponse<UserDto>> UpdateUserEmailAsync(Guid userId, string email);
    Task<ApiResponse<UserDto>> UpdateUserPasswordHashAsync(Guid userId, string passwordHash);
    Task<ApiResponse<UserDto>> UpdateUserDisplayNameAsync(Guid userId, string displayName);
    Task<ApiResponse<UserDto>> UpdateUserAvatarUrlAsync(Guid userId, string avatarUrl);
    Task<ApiResponse<UserSecurityDto>> UpdateTwoFactorAsync(Guid userId, UpdateTwoFactorPreferenceDto enable);

    Task<ApiResponse<bool>> DeleteUserAsync(Guid id);
    Task<ApiResponse<bool>> ActivateUserAsync(Guid userId);
    Task<ApiResponse<bool>> UserExistsAsync(Guid id);
    Task<ApiResponse<bool>> IsEmailUniqueAsync(string email, Guid? excludeUserId = null);
    Task<ApiResponse<bool>> IsUserActiveAsync(Guid userId);
}
