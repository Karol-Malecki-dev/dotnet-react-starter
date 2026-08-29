using Application.DTOs.Admin;
using Application.DTOs.User;
using Application.Interfaces;
using Domain.Enums;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Ocsp;
using Shared.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class DatabaseAdminService : IAdminService
    {
        private readonly ApplicationDbContext _dbContext;
        public DatabaseAdminService(ApplicationDbContext dbContext) 
        {
            _dbContext = dbContext;
        }

        //Get
        public async Task<ApiResponse<AdminDashboardStatsDto>> GetDashboardStatsAsync(CancellationToken cancellationToken = default)
        {
            var result = new AdminDashboardStatsDto();

            var totalUsers = await _dbContext.Users.AsNoTracking().CountAsync(cancellationToken);

            var activeUsers = await _dbContext.Users.AsNoTracking()
                .CountAsync(u => u.IsActive && u.Role == UserRole.User, cancellationToken);

            var inactiveUsers = await _dbContext.Users.AsNoTracking()
                .CountAsync(u => !u.IsActive && u.Role == UserRole.User, cancellationToken);

            var newUsersLast7Days = await _dbContext.Users.AsNoTracking()
                .CountAsync(u => u.CreatedAt >= DateTime.UtcNow.AddDays(-7), cancellationToken);

            var totalAdminUsers = await _dbContext.Users.AsNoTracking()
                .CountAsync(u => u.Role == UserRole.Admin, cancellationToken);

            var activeAdminUsers = await _dbContext.Users.AsNoTracking()
                .CountAsync(u => u.IsActive && u.Role == UserRole.Admin, cancellationToken);

            result.TotalUsers = totalUsers;
            result.ActiveUsers = activeUsers;
            result.InactiveUsers = inactiveUsers;
            result.NewUsersLast7Days = newUsersLast7Days;
            result.AdminUsers = totalAdminUsers;
            result.ActiveAdminUsers = activeAdminUsers;

            return ApiResponse<AdminDashboardStatsDto>.Success(result);
        }


        public async Task<ApiResponse<AdminUserDetailsDto>> GetUserDetailsByIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

            if (user == null)
                return ApiResponse<AdminUserDetailsDto>.Error(404, "User not found");

            var result = new AdminUserDetailsDto
            {
                Id = user.Id,
                Email = user.Email.Value,
                DisplayName = user.DisplayName.Value,
                AvatarUrl = user.AvatarUrl,
                IsActive = user.IsActive,
                IsEmailConfirmed = user.IsEmailConfirmed,
                IsTwoFactorEnabled = user.IsTwoFactorEnabled,
                Address = user.Address,
                Role = user.Role,
                CreatedAt = user.CreatedAt
            };

            return ApiResponse<AdminUserDetailsDto>.Success(result);
        }

        public async Task<ApiResponse<AdminUserDetailsDto>> GetUserDetailsByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            if (!EmailAddress.TryCreate(email, out var normalizedEmail) || normalizedEmail is null)
                return ApiResponse<AdminUserDetailsDto>.Error(400, "Email is required");

            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

            if (user == null)
                return ApiResponse<AdminUserDetailsDto>.Error(404, "User not found");

            var result = new AdminUserDetailsDto
            {
                Id = user.Id,
                Email = user.Email.Value,
                DisplayName = user.DisplayName.Value,
                AvatarUrl = user.AvatarUrl,
                IsActive = user.IsActive,
                IsEmailConfirmed = user.IsEmailConfirmed,
                IsTwoFactorEnabled = user.IsTwoFactorEnabled,
                Address = user.Address,
                Role = user.Role,
                CreatedAt = user.CreatedAt
            };

            return ApiResponse<AdminUserDetailsDto>.Success(result);
        }

        public async Task<ApiResponse<List<AdminUserListItemDto>>> GetUsersAsync(AdminUserFilterRequestDto request, CancellationToken cancellationToken = default)
        {
            var query = _dbContext.Users
                .AsNoTracking().
                AsQueryable();

            if(request.Ids is not null && request.Ids.Count > 0)
            {
                query = query.Where(u => request.Ids.Contains(u.Id));
            }

            if(request.Emails is not null && request.Emails.Count > 0)
            {
                var normalizedEmails = new List<EmailAddress>();
                foreach (var candidate in request.Emails)
                {
                    if (EmailAddress.TryCreate(candidate, out var normalizedEmail) && normalizedEmail is not null)
                    {
                        normalizedEmails.Add(normalizedEmail);
                    }
                }

                query = normalizedEmails.Count == 0
                    ? query.Where(_ => false)
                    : query.Where(u => normalizedEmails.Contains(u.Email));
            }
            
            if (request.Roles is not null && request.Roles.Count > 0) 
            {
                query = query.Where(u => request.Roles.Contains(u.Role));
            }

            if(request.IsActive.HasValue)
            {
                query = query.Where(u => u.IsActive == request.IsActive.Value);
            }

            if(request.IsEmailConfirmed.HasValue)
            {
                query = query.Where(u => u.IsEmailConfirmed == request.IsEmailConfirmed.Value);
            }
            if(request.IsTwoFactorEnabled.HasValue)
            {
                query = query.Where(u => u.IsTwoFactorEnabled == request.IsTwoFactorEnabled.Value);
            }

            var safePageNumber = Math.Max(request.PageNumber, 1);
            var safePageSize = Math.Clamp(request.PageSize, 1, 100);

            var users = await query
                .OrderBy(u => u.DisplayName)
                .Skip((safePageNumber - 1) * safePageSize)
                .Take(safePageSize)
                .ToListAsync(cancellationToken);

            var userDtos = users.Select(u => new AdminUserListItemDto
            {
                Id = u.Id,
                Email = u.Email.Value,
                DisplayName = u.DisplayName.Value,
                Role = u.Role.ToString(),
                IsActive = u.IsActive,
                IsEmailConfirmed = u.IsEmailConfirmed,
                CreatedAt = u.CreatedAt
            }).ToList();

            // return empty list as success when no items match the filter
            return ApiResponse<List<AdminUserListItemDto>>.Success(userDtos);
        }

        //Update
        public async Task<ApiResponse<AdminUserDetailsDto>> UpdateUserAsync(Guid userId, AdminUpdateUserRequestDto dto, CancellationToken cancellationToken = default)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (user == null)
                return ApiResponse<AdminUserDetailsDto>.Error(404, "User not found");

            // basic validation and normalization
            if (!string.IsNullOrWhiteSpace(dto.Email))
            {
                if (!EmailAddress.TryCreate(dto.Email, out var normalizedEmail) || normalizedEmail is null)
                    return ApiResponse<AdminUserDetailsDto>.Error(400, "Email has an invalid format");

                var emailInUse = await _dbContext.Users.AnyAsync(u => u.Email == normalizedEmail && u.Id != userId, cancellationToken);
                if (emailInUse)
                    return ApiResponse<AdminUserDetailsDto>.Error(400, "User with this email already exists");

                user.ChangeEmail(normalizedEmail);
            }

            if (!string.IsNullOrWhiteSpace(dto.DisplayName))
            {
                if (!DisplayName.TryCreate(dto.DisplayName, out var normalizedDisplayName) || normalizedDisplayName is null)
                    return ApiResponse<AdminUserDetailsDto>.Error(400, "Display name is invalid");

                user.ChangeDisplayName(normalizedDisplayName);
            }

            user.ChangeAvatarUrl(string.IsNullOrWhiteSpace(dto.AvatarUrl) ? null : dto.AvatarUrl.Trim());
            if (dto.IsActive)
            {
                user.Activate();
            }
            else
            {
                user.Deactivate();
            }

            user.SetEmailConfirmed(dto.IsEmailConfirmed);
            user.SetTwoFactorEnabled(dto.IsTwoFactorEnabled);
            user.ChangeAddress(string.IsNullOrWhiteSpace(dto.Address) ? null : dto.Address.Trim());

            await _dbContext.SaveChangesAsync(cancellationToken);

            var result = new AdminUserDetailsDto
            {
                Id = user.Id,
                Email = user.Email.Value,
                DisplayName = user.DisplayName.Value,
                AvatarUrl = user.AvatarUrl,
                IsActive = user.IsActive,
                IsEmailConfirmed = user.IsEmailConfirmed,
                IsTwoFactorEnabled = user.IsTwoFactorEnabled,
                Address = user.Address,
                Role = user.Role,
                CreatedAt = user.CreatedAt
            };

            return ApiResponse<AdminUserDetailsDto>.Success(result);
        }
        public async Task<ApiResponse<AdminUserDetailsDto>> UpdateUserRoleAsync(Guid userId, UserRole newRole, CancellationToken cancellationToken = default)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (user == null)
                return ApiResponse<AdminUserDetailsDto>.Error(404, "User not found");

            user.ChangeRole(newRole);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var result = new AdminUserDetailsDto
            {
                Id = user.Id,
                Email = user.Email.Value,
                DisplayName = user.DisplayName.Value,
                AvatarUrl = user.AvatarUrl,
                IsActive = user.IsActive,
                IsEmailConfirmed = user.IsEmailConfirmed,
                IsTwoFactorEnabled = user.IsTwoFactorEnabled,
                Address = user.Address,
                Role = user.Role,
                CreatedAt = user.CreatedAt
            };

            return ApiResponse<AdminUserDetailsDto>.Success(result);
        }

        public async Task<ApiResponse<AdminUserDetailsDto>> ActivateUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (user == null)
                return ApiResponse<AdminUserDetailsDto>.Error(404, "User not found");

            user.Activate();
            await _dbContext.SaveChangesAsync(cancellationToken);

            var result = new AdminUserDetailsDto
            {
                Id = user.Id,
                Email = user.Email.Value,
                DisplayName = user.DisplayName.Value,
                AvatarUrl = user.AvatarUrl,
                IsActive = user.IsActive,
                IsEmailConfirmed = user.IsEmailConfirmed,
                IsTwoFactorEnabled = user.IsTwoFactorEnabled,
                Address = user.Address,
                Role = user.Role,
                CreatedAt = user.CreatedAt
            };

            return ApiResponse<AdminUserDetailsDto>.Success(result);
        }

        public async Task<ApiResponse<AdminUserDetailsDto>> DeactivateUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (user == null)
                return ApiResponse<AdminUserDetailsDto>.Error(404, "User not found");

            user.Deactivate();
            await _dbContext.SaveChangesAsync(cancellationToken);

            var result = new AdminUserDetailsDto
            {
                Id = user.Id,
                Email = user.Email.Value,
                DisplayName = user.DisplayName.Value,
                AvatarUrl = user.AvatarUrl,
                IsActive = user.IsActive,
                IsEmailConfirmed = user.IsEmailConfirmed,
                IsTwoFactorEnabled = user.IsTwoFactorEnabled,
                Address = user.Address,
                Role = user.Role,
                CreatedAt = user.CreatedAt
            };

            return ApiResponse<AdminUserDetailsDto>.Success(result);
        }

        //Delete
        public async Task<ApiResponse<AdminUserDetailsDto>> DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (user == null)
                return ApiResponse<AdminUserDetailsDto>.Error(404, "User not found");

            var result = new AdminUserDetailsDto
            {
                Id = user.Id,
                Email = user.Email.Value,
                DisplayName = user.DisplayName.Value,
                AvatarUrl = user.AvatarUrl,
                IsActive = user.IsActive,
                IsEmailConfirmed = user.IsEmailConfirmed,
                IsTwoFactorEnabled = user.IsTwoFactorEnabled,
                Address = user.Address,
                Role = user.Role,
                CreatedAt = user.CreatedAt
            };

            _dbContext.Users.Remove(user);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ApiResponse<AdminUserDetailsDto>.Success(result);
        }
    }
}
