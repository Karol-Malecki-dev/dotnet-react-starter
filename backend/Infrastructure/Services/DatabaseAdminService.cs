using Application.DTOs.Admin;
using Application.DTOs.User;
using Application.Interfaces;
using Domain.Enums;
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
        public async Task<ApiResponse<AdminDashboardStatsDto>> GetDashboardStatsAsync()
        {
            var result = new AdminDashboardStatsDto();

            var totalUsers = await _dbContext.Users.AsNoTracking().CountAsync();

            var activeUsers = await _dbContext.Users.AsNoTracking()
                .CountAsync(u => u.IsActive && u.Role == UserRole.User);

            var inactiveUsers = await _dbContext.Users.AsNoTracking()
                .CountAsync(u => !u.IsActive && u.Role == UserRole.User);

            var newUsersLast7Days = await _dbContext.Users.AsNoTracking()
                .CountAsync(u => u.CreatedAt >= DateTime.UtcNow.AddDays(-7));

            var totalAdminUsers = await _dbContext.Users.AsNoTracking()
                .CountAsync(u => u.Role == UserRole.Admin);

            var activeAdminUsers = await _dbContext.Users.AsNoTracking()
                .CountAsync(u => u.IsActive && u.Role == UserRole.Admin);

            result.TotalUsers = totalUsers;
            result.ActiveUsers = activeUsers;
            result.InactiveUsers = inactiveUsers;
            result.NewUsersLast7Days = newUsersLast7Days;
            result.AdminUsers = totalAdminUsers;
            result.ActiveAdminUsers = activeAdminUsers;

            return ApiResponse<AdminDashboardStatsDto>.Success(result);
        }


        public async Task<ApiResponse<AdminUserDetailsDto>> GetUserDetailsByIdAsync(Guid userId)
        {
            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return ApiResponse<AdminUserDetailsDto>.Error(404, "User not found");

            var result = new AdminUserDetailsDto
            {
                Id = user.Id,
                Email = user.Email,
                DisplayName = user.DisplayName,
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

        public async Task<ApiResponse<AdminUserDetailsDto>> GetUserDetailsByEmailAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return ApiResponse<AdminUserDetailsDto>.Error(400, "Email is required");

            var normalized = email.Trim().ToLowerInvariant();

            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email.ToLower() == normalized);

            if (user == null)
                return ApiResponse<AdminUserDetailsDto>.Error(404, "User not found");

            var result = new AdminUserDetailsDto
            {
                Id = user.Id,
                Email = user.Email,
                DisplayName = user.DisplayName,
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

        public async Task<ApiResponse<List<AdminUserListItemDto>>> GetUsersAsync(AdminUserFilterRequestDto request)
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
                var normalizedEmails = request.Emails.
                Where(e => !string.IsNullOrWhiteSpace(e)).
                Select(e => e.Trim().ToLowerInvariant()).
                ToList();

                query = query.Where(u => normalizedEmails.Contains(u.Email.ToLowerInvariant()));
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
                .Select(u => new AdminUserListItemDto
                {
                    Id = u.Id,
                    Email = u.Email,
                    DisplayName = u.DisplayName,
                    Role = u.Role.ToString(),
                    IsActive = u.IsActive,
                    IsEmailConfirmed = u.IsEmailConfirmed,
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync();
            // return empty list as success when no items match the filter
            return ApiResponse<List<AdminUserListItemDto>>.Success(users);
        }

        //Update
        public async Task<ApiResponse<AdminUserDetailsDto>> UpdateUserAsync(Guid userId, AdminUpdateUserRequestDto dto)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                return ApiResponse<AdminUserDetailsDto>.Error(404, "User not found");

            // basic validation and normalization
            if (!string.IsNullOrWhiteSpace(dto.Email))
            {
                var normalized = dto.Email.Trim().ToLowerInvariant();
                var emailInUse = await _dbContext.Users.AnyAsync(u => u.Email.ToLower() == normalized && u.Id != userId);
                if (emailInUse)
                    return ApiResponse<AdminUserDetailsDto>.Error(400, "User with this email already exists");

                user.Email = dto.Email.Trim();
            }

            if (!string.IsNullOrWhiteSpace(dto.DisplayName))
                user.DisplayName = dto.DisplayName.Trim();

            user.AvatarUrl = string.IsNullOrWhiteSpace(dto.AvatarUrl) ? null : dto.AvatarUrl.Trim();
            user.IsActive = dto.IsActive;
            user.IsEmailConfirmed = dto.IsEmailConfirmed;
            user.IsTwoFactorEnabled = dto.IsTwoFactorEnabled;
            user.Address = string.IsNullOrWhiteSpace(dto.Address) ? null : dto.Address.Trim();

            await _dbContext.SaveChangesAsync();

            var result = new AdminUserDetailsDto
            {
                Id = user.Id,
                Email = user.Email,
                DisplayName = user.DisplayName,
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
        public async Task<ApiResponse<AdminUserDetailsDto>> UpdateUserRoleAsync(Guid userId, UserRole newRole)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                return ApiResponse<AdminUserDetailsDto>.Error(404, "User not found");

            user.Role = newRole;
            await _dbContext.SaveChangesAsync();

            var result = new AdminUserDetailsDto
            {
                Id = user.Id,
                Email = user.Email,
                DisplayName = user.DisplayName,
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

        public async Task<ApiResponse<AdminUserDetailsDto>> ActivateUserAsync(Guid userId)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                return ApiResponse<AdminUserDetailsDto>.Error(404, "User not found");

            user.IsActive = true;
            await _dbContext.SaveChangesAsync();

            var result = new AdminUserDetailsDto
            {
                Id = user.Id,
                Email = user.Email,
                DisplayName = user.DisplayName,
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

        public async Task<ApiResponse<AdminUserDetailsDto>> DeactivateUserAsync(Guid userId)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                return ApiResponse<AdminUserDetailsDto>.Error(404, "User not found");

            user.IsActive = false;
            await _dbContext.SaveChangesAsync();

            var result = new AdminUserDetailsDto
            {
                Id = user.Id,
                Email = user.Email,
                DisplayName = user.DisplayName,
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
        public async Task<ApiResponse<AdminUserDetailsDto>> DeleteUserAsync(Guid userId)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                return ApiResponse<AdminUserDetailsDto>.Error(404, "User not found");

            var result = new AdminUserDetailsDto
            {
                Id = user.Id,
                Email = user.Email,
                DisplayName = user.DisplayName,
                AvatarUrl = user.AvatarUrl,
                IsActive = user.IsActive,
                IsEmailConfirmed = user.IsEmailConfirmed,
                IsTwoFactorEnabled = user.IsTwoFactorEnabled,
                Address = user.Address,
                Role = user.Role,
                CreatedAt = user.CreatedAt
            };

            _dbContext.Users.Remove(user);
            await _dbContext.SaveChangesAsync();
            return ApiResponse<AdminUserDetailsDto>.Success(result);
        }
    }
}
