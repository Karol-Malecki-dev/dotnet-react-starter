using Application.DTOs.Admin;
using Application.DTOs.User;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shared.Responses;

namespace Infrastructure.Services;

public class DatabaseUserService : IUserService
{
    private readonly ApplicationDbContext _dbContext;

    public DatabaseUserService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ApiResponse<UserDto>> GetUserByIdAsync(Guid id)
    {
        var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return user is null
            ? ApiResponse<UserDto>.Error(404, "User not found")
            : ApiResponse<UserDto>.Success(MapToDto(user));
    }

    public async Task<ApiResponse<UserDto>> GetUserByEmailAsync(string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Email == normalizedEmail);
        return user is null
            ? ApiResponse<UserDto>.Error(404, "User not found")
            : ApiResponse<UserDto>.Success(MapToDto(user));
    }

    public async Task<ApiResponse<List<UserDto>>> GetAllUsersPagedAsync(int pageNumber, int pageSize)
    {
        var safePageNumber = Math.Max(pageNumber, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 100);

        var users = await _dbContext.Users
            .AsNoTracking()
            .OrderBy(x => x.Email)
            .Skip((safePageNumber - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync();

        return ApiResponse<List<UserDto>>.Success(users.Select(MapToDto).ToList());
    }

    public async Task<ApiResponse<int>> GetUserCountAsync()
    {
        var count = await _dbContext.Users.CountAsync();
        return ApiResponse<int>.Success(count);
    }

    public async Task<ApiResponse<UserDto>> UpdateUserAsync(Guid userId, UpdateUserDto dto)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
        {
            return ApiResponse<UserDto>.Error(404, "User not found");
        }

        var (currentFirstName, currentLastName) = SplitDisplayName(user.DisplayName);

        if (dto.FirstName is not null || dto.LastName is not null)
        {
            var firstName = dto.FirstName is null ? currentFirstName : dto.FirstName.Trim();
            var lastName = dto.LastName is null ? currentLastName : dto.LastName.Trim();
            var displayName = $"{firstName} {lastName}".Trim();

            if (string.IsNullOrWhiteSpace(displayName))
            {
                return ApiResponse<UserDto>.Error(400, "First name or last name is required");
            }

            user.DisplayName = displayName;
        }

        if (dto.Email is not null)
        {
            if (string.IsNullOrWhiteSpace(dto.Email))
            {
                return ApiResponse<UserDto>.Error(400, "Email is required");
            }

            var normalizedEmail = NormalizeEmail(dto.Email);
            var emailInUse = await _dbContext.Users.AnyAsync(x => x.Email == normalizedEmail && x.Id != userId);
            if (emailInUse)
            {
                return ApiResponse<UserDto>.Error(400, "User with this email already exists");
            }

            user.Email = normalizedEmail;
        }

        if (dto.AvatarUrl is not null)
        {
            var avatarUrl = dto.AvatarUrl.Trim();

            if (string.IsNullOrWhiteSpace(avatarUrl))
            {
                user.AvatarUrl = null;
            }
            else if (!IsValidHttpUrl(avatarUrl))
            {
                return ApiResponse<UserDto>.Error(400, "Avatar URL must be a valid absolute http or https URL");
            }
            else
            {
                user.AvatarUrl = avatarUrl;
            }
        }

        await _dbContext.SaveChangesAsync();

        return ApiResponse<UserDto>.Success(MapToDto(user), "User profile updated");
    }

    public async Task<ApiResponse<bool>> DeactivateUserAsync(Guid userId)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
        {
            return ApiResponse<bool>.Error(404, "User not found");
        }

        user.IsActive = false;
        await _dbContext.SaveChangesAsync();

        return ApiResponse<bool>.Success(true, "User deactivated");
    }

    public async Task<ApiResponse<bool>> ActivateUserAsync(Guid userId)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
        {
            return ApiResponse<bool>.Error(404, "User not found");
        }

        user.IsActive = true;
        await _dbContext.SaveChangesAsync();

        return ApiResponse<bool>.Success(true, "User activated");
    }

    public async Task<ApiResponse<bool>> DeleteUserAsync(Guid id)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (user is null)
        {
            return ApiResponse<bool>.Error(404, "User not found");
        }

        _dbContext.Users.Remove(user);
        await _dbContext.SaveChangesAsync();

        return ApiResponse<bool>.Success(true, "User deleted");
    }

    public async Task<ApiResponse<UserDto>> UpdateDisplayNameAsync(Guid userId, string displayName)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
        {
            return ApiResponse<UserDto>.Error(404, "User not found");
        }

        user.DisplayName = displayName.Trim();
        await _dbContext.SaveChangesAsync();

        return ApiResponse<UserDto>.Success(MapToDto(user), "Display name updated");
    }

    public async Task<ApiResponse<UserDto>> UpdateUserRoleAsync(Guid userId, string role)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
        {
            return ApiResponse<UserDto>.Error(404, "User not found");
        }

        if (!Enum.TryParse<UserRole>(role, ignoreCase: true, out var parsedRole))
        {
            return ApiResponse<UserDto>.Error(400, "Invalid user role");
        }

        user.Role = parsedRole;
        await _dbContext.SaveChangesAsync();

        return ApiResponse<UserDto>.Success(MapToDto(user), "User role updated");
    }

    public async Task<ApiResponse<bool>> UserExistsAsync(Guid id)
    {
        var exists = await _dbContext.Users.AnyAsync(x => x.Id == id);
        return ApiResponse<bool>.Success(exists);
    }

    public async Task<ApiResponse<bool>> IsEmailUniqueAsync(string email, Guid? excludeUserId = null)
    {
        var normalizedEmail = NormalizeEmail(email);
        var exists = await _dbContext.Users.AnyAsync(x => x.Email == normalizedEmail && (!excludeUserId.HasValue || x.Id != excludeUserId.Value));
        return ApiResponse<bool>.Success(!exists);
    }

    public async Task<ApiResponse<bool>> IsUserActiveAsync(Guid userId)
    {
        var isActive = await _dbContext.Users.AnyAsync(x => x.Id == userId && x.IsActive);
        return ApiResponse<bool>.Success(isActive);
    }

    public async Task<ApiResponse<string>> GetUserRoleAsync(Guid userId)
    {
        var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId);
        return user is null
            ? ApiResponse<string>.Error(404, "User not found")
            : ApiResponse<string>.Success(user.Role.ToString());
    }

    private static string NormalizeEmail(string email)
        => email.Trim().ToLowerInvariant();

    private static UserDto MapToDto(User user)
    {
        var (firstName, lastName) = SplitDisplayName(user.DisplayName);

        return new UserDto
        {
            Id = user.Id,
            FirstName = firstName,
            LastName = lastName,
            DisplayName = user.DisplayName,
            Email = user.Email,
            AvatarUrl = user.AvatarUrl,
            Role = user.Role.ToString(),
            PhoneNumber = string.Empty,
            Address = string.Empty,
            CreatedAt = user.CreatedAt
        };
    }
    private static UserSecurityDto MapToSecurityDto(User user)
    {
        return new UserSecurityDto
        {
            Email = user.Email,
            IsEmailConfirmed = user.IsEmailConfirmed,
            IsTwoFactorEnabled = user.IsTwoFactorEnabled
        };
    }
    private static (string FirstName, string LastName) SplitDisplayName(string displayName)
    {
        var parts = displayName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return (
            parts.Length > 0 ? parts[0] : string.Empty,
            parts.Length > 1 ? parts[1] : string.Empty);
    }

    private static bool IsValidHttpUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var parsed)
            && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps);
    }

    public async Task<ApiResponse<UserDto>> UpdateUserEmailAsync(Guid userId, string email)
    {
        var user = _dbContext.Users.FirstOrDefault(x => x.Id == userId);
        if (user is null)
        {
            return ApiResponse<UserDto>.Error(404, "User not found");
        }

        user.Email = NormalizeEmail(email);
        await _dbContext.SaveChangesAsync();

        return ApiResponse<UserDto>.Success(MapToDto(user), "Email updated");
    }

    public async Task<ApiResponse<UserDto>> UpdateUserPasswordHashAsync(Guid userId, string passwordHash)
    {
        var user = _dbContext.Users.FirstOrDefault(x => x.Id == userId);
        if (user is null)
        {
            return ApiResponse<UserDto>.Error(404, "User not found");
        }
        user.PasswordHash = passwordHash;
        await _dbContext.SaveChangesAsync();

        return ApiResponse<UserDto>.Success(MapToDto(user), "Password updated");
    }

    public async Task<ApiResponse<UserDto>> UpdateUserDisplayNameAsync(Guid userId, string displayName)
    {
        var user = _dbContext.Users.FirstOrDefault(x => x.Id == userId);
        if (user is null)
        {
            return ApiResponse<UserDto>.Error(404, "User not found");
        }
        user.DisplayName = displayName;
        await _dbContext.SaveChangesAsync();

        return ApiResponse<UserDto>.Success(MapToDto(user), "Display name updated");
    }

    public async Task<ApiResponse<UserDto>> UpdateUserAvatarUrlAsync(Guid userId, string avatarUrl)
    {
        var user = _dbContext.Users.FirstOrDefault(x => x.Id == userId);
        if (user is null)
        {
            return ApiResponse<UserDto>.Error(404, "User not found");
        }
        user.AvatarUrl = avatarUrl;
        await _dbContext.SaveChangesAsync();

        return ApiResponse<UserDto>.Success(MapToDto(user), "Avatar URL updated");
    }

    public async Task<ApiResponse<UserSecurityDto>> UpdateTwoFactorAsync(Guid userId, UpdateTwoFactorPreferenceDto enable)
    {

        var user = _dbContext.Users.FirstOrDefault(x => x.Id == userId);
        if (user is null)
        {
            return ApiResponse<UserSecurityDto>.Error(404, "User not found");
        }

        if(enable.Enable && !user.IsEmailConfirmed)
        {
            return ApiResponse<UserSecurityDto>.Error(400, "Email must be confirmed before enabling two-factor authentication");
        }

        user.IsTwoFactorEnabled = enable.Enable;
        await _dbContext.SaveChangesAsync();
        return ApiResponse<UserSecurityDto>.Success(
            MapToSecurityDto(user),
            $"Two-factor authentication {(enable.Enable ? "enabled" : "disabled")} successfully"); 
    }

    public async Task<ApiResponse<bool>> IsTwoFactorEnabled(Guid userId)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
        {
            return ApiResponse<bool>.Error(404, "User not found");
        }
        return ApiResponse<bool>.Success(user.IsTwoFactorEnabled);
    }

    public async Task<ApiResponse<UserSecurityDto>> GetUserSecurityAsync(Guid userId)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
        {
            return ApiResponse<UserSecurityDto>.Error(404, "User not found");
        }

        return ApiResponse<UserSecurityDto>.Success(MapToSecurityDto(user));
    }

    public Task<ApiResponse<AdminDashboardStatsDto>> GetAdminDashboardStatsAsync(Guid userID)
    {
        throw new NotImplementedException();
    }
}

