using Application.DTOs.Admin;
using Application.DTOs.User;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
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

    public async Task<ApiResponse<UserDto>> GetUserByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return user is null
            ? ApiResponse<UserDto>.Error(404, "User not found")
            : ApiResponse<UserDto>.Success(MapToDto(user));
    }

    public async Task<ApiResponse<UserDto>> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (!EmailAddress.TryCreate(email, out var normalizedEmail) || normalizedEmail is null)
        {
            return ApiResponse<UserDto>.Error(400, "Email has an invalid format");
        }

        var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);
        return user is null
            ? ApiResponse<UserDto>.Error(404, "User not found")
            : ApiResponse<UserDto>.Success(MapToDto(user));
    }

    public async Task<ApiResponse<List<UserDto>>> GetAllUsersPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var safePageNumber = Math.Max(pageNumber, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 100);

        var users = await _dbContext.Users
            .AsNoTracking()
            .OrderBy(x => x.Email)
            .Skip((safePageNumber - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(cancellationToken);

        return ApiResponse<List<UserDto>>.Success(users.Select(MapToDto).ToList());
    }

    public async Task<ApiResponse<int>> GetUserCountAsync(CancellationToken cancellationToken = default)
    {
        var count = await _dbContext.Users.CountAsync(cancellationToken);
        return ApiResponse<int>.Success(count);
    }

    public async Task<ApiResponse<UserDto>> UpdateUserAsync(Guid userId, UpdateUserDto dto, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return ApiResponse<UserDto>.Error(404, "User not found");
        }

        var (currentFirstName, currentLastName) = SplitDisplayName(user.DisplayName.Value);

        if (dto.FirstName is not null || dto.LastName is not null)
        {
            var firstName = dto.FirstName is null ? currentFirstName : dto.FirstName.Trim();
            var lastName = dto.LastName is null ? currentLastName : dto.LastName.Trim();
            var displayName = $"{firstName} {lastName}".Trim();

            if (string.IsNullOrWhiteSpace(displayName))
            {
                return ApiResponse<UserDto>.Error(400, "First name or last name is required");
            }

            if (!DisplayName.TryCreate(displayName, out var normalizedDisplayName) || normalizedDisplayName is null)
            {
                return ApiResponse<UserDto>.Error(400, "Display name is invalid");
            }

            user.ChangeDisplayName(normalizedDisplayName);
        }

        if (dto.Email is not null)
        {
            if (string.IsNullOrWhiteSpace(dto.Email))
            {
                return ApiResponse<UserDto>.Error(400, "Email is required");
            }

            if (!EmailAddress.TryCreate(dto.Email, out var normalizedEmail) || normalizedEmail is null)
            {
                return ApiResponse<UserDto>.Error(400, "Email has an invalid format");
            }

            var emailInUse = await _dbContext.Users.AnyAsync(x => x.Email == normalizedEmail && x.Id != userId, cancellationToken);
            if (emailInUse)
            {
                return ApiResponse<UserDto>.Error(400, "User with this email already exists");
            }

            user.ChangeEmail(normalizedEmail);
        }

        if (dto.AvatarUrl is not null)
        {
            var avatarUrl = dto.AvatarUrl.Trim();

            if (string.IsNullOrWhiteSpace(avatarUrl))
            {
                user.ChangeAvatarUrl(null);
            }
            else if (!IsValidHttpUrl(avatarUrl))
            {
                return ApiResponse<UserDto>.Error(400, "Avatar URL must be a valid absolute http or https URL");
            }
            else
            {
                user.ChangeAvatarUrl(avatarUrl);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ApiResponse<UserDto>.Success(MapToDto(user), "User profile updated");
    }

    public async Task<ApiResponse<bool>> DeactivateUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return ApiResponse<bool>.Error(404, "User not found");
        }

        user.Deactivate();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.Success(true, "User deactivated");
    }

    public async Task<ApiResponse<bool>> ActivateUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return ApiResponse<bool>.Error(404, "User not found");
        }

        user.Activate();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.Success(true, "User activated");
    }

    public async Task<ApiResponse<bool>> DeleteUserAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null)
        {
            return ApiResponse<bool>.Error(404, "User not found");
        }

        _dbContext.Users.Remove(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.Success(true, "User deleted");
    }

    public async Task<ApiResponse<UserDto>> UpdateDisplayNameAsync(Guid userId, string displayName, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return ApiResponse<UserDto>.Error(404, "User not found");
        }

        if (!DisplayName.TryCreate(displayName, out var normalizedDisplayName) || normalizedDisplayName is null)
        {
            return ApiResponse<UserDto>.Error(400, "Display name is invalid");
        }

        user.ChangeDisplayName(normalizedDisplayName);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ApiResponse<UserDto>.Success(MapToDto(user), "Display name updated");
    }

    public async Task<ApiResponse<UserDto>> UpdateUserRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return ApiResponse<UserDto>.Error(404, "User not found");
        }

        if (!Enum.TryParse<UserRole>(role, ignoreCase: true, out var parsedRole))
        {
            return ApiResponse<UserDto>.Error(400, "Invalid user role");
        }

        user.ChangeRole(parsedRole);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ApiResponse<UserDto>.Success(MapToDto(user), "User role updated");
    }

    public async Task<ApiResponse<bool>> UserExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var exists = await _dbContext.Users.AnyAsync(x => x.Id == id, cancellationToken);
        return ApiResponse<bool>.Success(exists);
    }

    public async Task<ApiResponse<bool>> IsEmailUniqueAsync(string email, Guid? excludeUserId = null, CancellationToken cancellationToken = default)
    {
        if (!EmailAddress.TryCreate(email, out var normalizedEmail) || normalizedEmail is null)
        {
            return ApiResponse<bool>.Error(400, "Email has an invalid format");
        }

        var exists = await _dbContext.Users.AnyAsync(x => x.Email == normalizedEmail && (!excludeUserId.HasValue || x.Id != excludeUserId.Value), cancellationToken);
        return ApiResponse<bool>.Success(!exists);
    }

    public async Task<ApiResponse<bool>> IsUserActiveAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var isActive = await _dbContext.Users.AnyAsync(x => x.Id == userId && x.IsActive, cancellationToken);
        return ApiResponse<bool>.Success(isActive);
    }

    public async Task<ApiResponse<string>> GetUserRoleAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        return user is null
            ? ApiResponse<string>.Error(404, "User not found")
            : ApiResponse<string>.Success(user.Role.ToString());
    }

    private static UserDto MapToDto(User user)
    {
        var (firstName, lastName) = SplitDisplayName(user.DisplayName.Value);

        return new UserDto
        {
            Id = user.Id,
            FirstName = firstName,
            LastName = lastName,
            DisplayName = user.DisplayName.Value,
            Email = user.Email.Value,
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
            Email = user.Email.Value,
            IsEmailConfirmed = user.IsEmailConfirmed,
            IsTwoFactorEnabled = user.IsTwoFactorEnabled,
            IsAuthenticatorEnabled = user.IsAuthenticatorEnabled
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

    public async Task<ApiResponse<UserDto>> UpdateUserEmailAsync(Guid userId, string email, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return ApiResponse<UserDto>.Error(404, "User not found");
        }

        if (!EmailAddress.TryCreate(email, out var normalizedEmail) || normalizedEmail is null)
        {
            return ApiResponse<UserDto>.Error(400, "Email has an invalid format");
        }

        user.ChangeEmail(normalizedEmail);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ApiResponse<UserDto>.Success(MapToDto(user), "Email updated");
    }

    public async Task<ApiResponse<UserDto>> UpdateUserPasswordHashAsync(Guid userId, string passwordHash, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return ApiResponse<UserDto>.Error(404, "User not found");
        }
        user.SetPasswordHash(passwordHash);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ApiResponse<UserDto>.Success(MapToDto(user), "Password updated");
    }

    public async Task<ApiResponse<UserDto>> UpdateUserDisplayNameAsync(Guid userId, string displayName, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return ApiResponse<UserDto>.Error(404, "User not found");
        }
        if (!DisplayName.TryCreate(displayName, out var normalizedDisplayName) || normalizedDisplayName is null)
        {
            return ApiResponse<UserDto>.Error(400, "Display name is invalid");
        }

        user.ChangeDisplayName(normalizedDisplayName);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ApiResponse<UserDto>.Success(MapToDto(user), "Display name updated");
    }

    public async Task<ApiResponse<UserDto>> UpdateUserAvatarUrlAsync(Guid userId, string avatarUrl, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return ApiResponse<UserDto>.Error(404, "User not found");
        }
        user.ChangeAvatarUrl(avatarUrl);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ApiResponse<UserDto>.Success(MapToDto(user), "Avatar URL updated");
    }

    public async Task<ApiResponse<UserSecurityDto>> UpdateTwoFactorAsync(Guid userId, UpdateTwoFactorPreferenceDto enable, CancellationToken cancellationToken = default)
    {

        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return ApiResponse<UserSecurityDto>.Error(404, "User not found");
        }

        if(enable.Enable && !user.IsEmailConfirmed)
        {
            return ApiResponse<UserSecurityDto>.Error(400, "Email must be confirmed before enabling two-factor authentication");
        }

        user.SetTwoFactorEnabled(enable.Enable);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ApiResponse<UserSecurityDto>.Success(
            MapToSecurityDto(user),
            $"Two-factor authentication {(enable.Enable ? "enabled" : "disabled")} successfully"); 
    }

    public async Task<ApiResponse<bool>> IsTwoFactorEnabled(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return ApiResponse<bool>.Error(404, "User not found");
        }
        return ApiResponse<bool>.Success(user.IsTwoFactorEnabled);
    }

    public async Task<ApiResponse<UserSecurityDto>> GetUserSecurityAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return ApiResponse<UserSecurityDto>.Error(404, "User not found");
        }

        return ApiResponse<UserSecurityDto>.Success(MapToSecurityDto(user));
    }

}
