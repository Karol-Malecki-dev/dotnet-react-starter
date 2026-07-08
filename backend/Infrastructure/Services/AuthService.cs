using Application.Interfaces;
using Domain.Entities;
using Domain.Entities.Auth;
using Domain.Enums;
using Domain.Enums.Auth;
using Domain.Interfaces;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<AuthService> _logger;
    private readonly IUserService _userService;

    private readonly PasswordHasher<User> _passwordHasher = new();

    private static readonly ConcurrentDictionary<string, (Guid UserId, DateTime ExpiresAt)> PasswordResetTokens = new();
    private static readonly ConcurrentDictionary<string, (Guid UserId, DateTime ExpiresAt)> EmailConfirmationTokens = new();

    public AuthService(ApplicationDbContext dbContext, ILogger<AuthService> logger, IUserService userService)
    {
        _dbContext = dbContext;
        _logger = logger;
        _userService = userService;
    }

    public async Task<User?> AuthenticateAsync(string email, string password)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        if (user == null || !user.IsActive)
        {
            return null;
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return null;
        }

        if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, password);
            await _dbContext.SaveChangesAsync();
        }

        return user;
    }

    public async Task<User?> RegisterAsync(string email, string password, string displayName)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        var exists = await _dbContext.Users.AnyAsync(u => u.Email == normalizedEmail);
        if (exists)
        {
            return null;
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            PasswordHash = string.Empty,
            DisplayName = displayName.Trim(),
            Role = UserRole.User,
            IsActive = true,
            IsEmailConfirmed = false,
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, password);

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        return user;
    }

    public Task<bool> LogoutAsync(Guid userId)
    {
        return Task.FromResult(true);
    }

    public Task<bool> UserExistsAsync(string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return _dbContext.Users.AnyAsync(u => u.Email == normalizedEmail);
    }

    public Task<bool> IsEmailConfirmedAsync(Guid userId)
    {
        return _dbContext.Users.AnyAsync(u => u.Id == userId && u.IsEmailConfirmed);
    }

    public Task<bool> IsUserActiveAsync(Guid userId)
    {
        return _dbContext.Users.AnyAsync(u => u.Id == userId && u.IsActive);
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            return false;
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return false;
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
        await _dbContext.SaveChangesAsync();

        return true;
    }

    public async Task<string?> GeneratePasswordResetTokenAsync(string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == normalizedEmail);
        if (user == null)
        {
            return null;
        }

        var token = Guid.NewGuid().ToString("N");
        PasswordResetTokens[token] = (user.Id, DateTime.UtcNow.AddHours(1));

        return token;
    }

    public async Task<bool> SendPasswordResetEmailAsync(string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return await _dbContext.Users.AnyAsync(u => u.Email == normalizedEmail);
    }

    public async Task<bool> ResetPasswordAsync(string email, string resetToken, string newPassword)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
        if (user == null)
        {
            return false;
        }

        if (!PasswordResetTokens.TryGetValue(resetToken, out var tokenData))
        {
            return false;
        }

        if (tokenData.UserId != user.Id || tokenData.ExpiresAt < DateTime.UtcNow)
        {
            PasswordResetTokens.TryRemove(resetToken, out _);
            return false;
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
        await _dbContext.SaveChangesAsync();
        PasswordResetTokens.TryRemove(resetToken, out _);

        return true;
    }

    public async Task<string?> GenerateEmailConfirmationTokenAsync(Guid userId)
    {
        var userExists = await _dbContext.Users.AsNoTracking().AnyAsync(u => u.Id == userId);
        if (!userExists)
        {
            return null;
        }

        var token = Guid.NewGuid().ToString("N");
        EmailConfirmationTokens[token] = (userId, DateTime.UtcNow.AddDays(1));

        return token;
    }

    public async Task<bool> ConfirmEmailAsync(Guid userId, string confirmationToken)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            return false;
        }

        if (!EmailConfirmationTokens.TryGetValue(confirmationToken, out var tokenData))
        {
            return false;
        }

        if (tokenData.UserId != userId || tokenData.ExpiresAt < DateTime.UtcNow)
        {
            EmailConfirmationTokens.TryRemove(confirmationToken, out _);
            return false;
        }

        user.IsEmailConfirmed = true;
        await _dbContext.SaveChangesAsync();
        EmailConfirmationTokens.TryRemove(confirmationToken, out _);

        _logger.LogInformation("Email confirmed for user {UserId}", userId);
        return true;
    }

    public async Task<bool> ConfirmEmailConfirmedAsync(string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return await _dbContext.Users.AnyAsync(u => u.Email == normalizedEmail && u.IsEmailConfirmed);
    }

    public Task<EmailTwoFactorChallengeDelivery?> CreateEmailTwoFactorChallengeAsync(Guid userId)
    {
        return Task.FromResult<EmailTwoFactorChallengeDelivery?>(null);
    }

    public Task<User?> VerifyEmailTwoFactorChallengeAsync(Guid challengeId, string code)
    {
        return Task.FromResult<User?>(null);
    }

    public Task<EmailTwoFactorChallengeDelivery?> ResendEmailTwoFactorChallengeAsync(Guid challengeId)
    {
        return Task.FromResult<EmailTwoFactorChallengeDelivery?>(null);
    }

    public Task<bool> StartResetPasswordBySendingTokenToEmailAsync(string email, ResetType resetType)
    {
        throw new NotImplementedException();
    }

    public Task<bool> EndResetPasswordByVerificationTokenAsync(string email, ResetType resetType, string token, string code, string hashedPassword)
    {
        throw new NotImplementedException();
    }
}
