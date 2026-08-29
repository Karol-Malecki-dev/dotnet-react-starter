using Application.DTOs.Admin;
using Application.DTOs.User;
using Shared.Responses;

namespace Application.Interfaces;

public interface IUserService
{
    Task<ApiResponse<UserDto>> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ApiResponse<UserDto>> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<UserDto>>> GetAllUsersPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<ApiResponse<int>> GetUserCountAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<string>> GetUserRoleAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ApiResponse<UserSecurityDto>> GetUserSecurityAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<ApiResponse<UserDto>> UpdateUserAsync(Guid userId, UpdateUserDto dto, CancellationToken cancellationToken = default);
    Task<ApiResponse<UserDto>> UpdateDisplayNameAsync(Guid userId, string displayName, CancellationToken cancellationToken = default);
    Task<ApiResponse<UserDto>> UpdateUserRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default);
    Task<ApiResponse<UserDto>> UpdateUserEmailAsync(Guid userId, string email, CancellationToken cancellationToken = default);
    Task<ApiResponse<UserDto>> UpdateUserPasswordHashAsync(Guid userId, string passwordHash, CancellationToken cancellationToken = default);
    Task<ApiResponse<UserDto>> UpdateUserDisplayNameAsync(Guid userId, string displayName, CancellationToken cancellationToken = default);
    Task<ApiResponse<UserDto>> UpdateUserAvatarUrlAsync(Guid userId, string avatarUrl, CancellationToken cancellationToken = default);
    Task<ApiResponse<UserSecurityDto>> UpdateTwoFactorAsync(Guid userId, UpdateTwoFactorPreferenceDto enable, CancellationToken cancellationToken = default);

    Task<ApiResponse<bool>> DeleteUserAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> ActivateUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> UserExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> IsEmailUniqueAsync(string email, Guid? excludeUserId = null, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> IsUserActiveAsync(Guid userId, CancellationToken cancellationToken = default);
}
