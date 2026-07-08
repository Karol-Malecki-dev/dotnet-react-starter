using Domain.Entities;
using Domain.Entities.Auth;
using Domain.Enums;
using Domain.Enums.Auth;
using Domain.Interfaces;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Settings;
using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Services;

public class DatabaseAuthService : IAuthService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly EmailConfirmationSettings _emailConfirmationSettings;
    private readonly EmailTwoFactorSettings _emailTwoFactorSettings;
    private readonly ILogger<DatabaseAuthService> _logger;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public DatabaseAuthService(
        ApplicationDbContext dbContext,
        IOptions<EmailConfirmationSettings> emailConfirmationOptions,
        IOptions<EmailTwoFactorSettings> emailTwoFactorOptions,
        ILogger<DatabaseAuthService> logger)
    {
        _dbContext = dbContext;
        _emailConfirmationSettings = emailConfirmationOptions.Value;
        _emailTwoFactorSettings = emailTwoFactorOptions.Value;
        _logger = logger;
    }

    public async Task<User?> AuthenticateAsync(string email, string password)
    {
        var normalizedEmail = NormalizeEmail(email);
        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Email == normalizedEmail);

        if (user is null || !user.IsActive)
        {
            _logger.LogWarning("Authentication failed for {Email}", normalizedEmail);
            return null;
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            _logger.LogWarning("Authentication failed for {Email}", normalizedEmail);
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
        var normalizedEmail = NormalizeEmail(email);
        if (await _dbContext.Users.AnyAsync(x => x.Email == normalizedEmail))
        {
            return null;
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            DisplayName = displayName.Trim(),
            Role = UserRole.User,
            IsActive = true,
            IsEmailConfirmed = false,
            IsTwoFactorEnabled = _emailTwoFactorSettings.Enabled && _emailTwoFactorSettings.EnableForNewUsers,
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, password);

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Registered user {UserId} ({Email})", user.Id, user.Email);
        return user;
    }

    public Task<bool> LogoutAsync(Guid userId)
        => Task.FromResult(true);

    public async Task<bool> UserExistsAsync(string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        return await _dbContext.Users.AnyAsync(x => x.Email == normalizedEmail);
    }

    public async Task<bool> IsEmailConfirmedAsync(Guid userId)
        => await _dbContext.Users.AnyAsync(x => x.Id == userId && x.IsEmailConfirmed);

    public async Task<bool> IsUserActiveAsync(Guid userId)
        => await _dbContext.Users.AnyAsync(x => x.Id == userId && x.IsActive);

    public async Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
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

    public async Task<bool> SendPasswordResetEmailAsync(string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return false;
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Email == normalizedEmail);
        if (user is null)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        var activeRequests = await _dbContext.PasswordResetRequests
            .Where(x => x.UserId == user.Id && x.ConsumedAt == null && x.RevokedAt == null && x.ExpiresAt > now)
            .ToListAsync();

        foreach (var activeRequest in activeRequests)
        {
            activeRequest.RevokedAt = now;
        }

        var rawToken = GenerateSecureToken();
        var resetRequest = new PasswordResetRequest
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            ResetType = ResetType.Link,
            TokenHash = HashToken(rawToken),
            CodeHash = string.Empty,
            CreatedAt = now,
            LastSentAt = now,
            ExpiresAt = now.AddMinutes(_emailTwoFactorSettings.CodeExpiresInMinutes),
            FailedAttempts = 0
        };

        _dbContext.PasswordResetRequests.Add(resetRequest);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Password reset request created for user {UserId} ({Email})", user.Id, user.Email);
        return true;
    }

    public Task<string?> GeneratePasswordResetTokenAsync(string email)
        => Task.FromResult<string?>(null);

    public async Task<bool> ResetPasswordAsync(string email, string resetToken, string newPassword)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(resetToken) || string.IsNullOrWhiteSpace(newPassword))
        {
            return false;
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Email == normalizedEmail);
        if (user is null)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        var tokenHash = HashToken(resetToken.Trim());
        var resetRequest = await _dbContext.PasswordResetRequests
            .FirstOrDefaultAsync(x =>
                x.UserId == user.Id &&
                x.ResetType == ResetType.Link &&
                x.TokenHash == tokenHash &&
                x.ConsumedAt == null &&
                x.RevokedAt == null);

        if (resetRequest is null)
        {
            return false;
        }

        if (resetRequest.ExpiresAt <= now)
        {
            resetRequest.RevokedAt = now;
            await _dbContext.SaveChangesAsync();
            return false;
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
        resetRequest.ConsumedAt = now;

        var remainingRequests = await _dbContext.PasswordResetRequests
            .Where(x => x.UserId == user.Id && x.Id != resetRequest.Id && x.ConsumedAt == null && x.RevokedAt == null)
            .ToListAsync();

        foreach (var remainingRequest in remainingRequests)
        {
            remainingRequest.RevokedAt = now;
        }

        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<string?> GenerateEmailConfirmationTokenAsync(Guid userId)
    {
        var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null || user.IsEmailConfirmed)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var activeTokens = await _dbContext.EmailConfirmationTokens
            .Where(x => x.UserId == userId && x.ConsumedAt == null && x.RevokedAt == null && x.ExpiresAt > now)
            .ToListAsync();

        foreach (var activeToken in activeTokens)
        {
            activeToken.RevokedAt = now;
        }

        var rawToken = GenerateSecureToken();
        _dbContext.EmailConfirmationTokens.Add(new EmailConfirmationToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = HashToken(rawToken),
            CreatedAt = now,
            ExpiresAt = now.AddHours(_emailConfirmationSettings.TokenExpiresInHours)
        });

        await _dbContext.SaveChangesAsync();
        return rawToken;
    }

    public async Task<bool> ConfirmEmailAsync(Guid userId, string confirmationToken)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(confirmationToken))
        {
            return false;
        }

        var now = DateTime.UtcNow;
        var tokenHash = HashToken(confirmationToken);
        var token = await _dbContext.EmailConfirmationTokens
            .FirstOrDefaultAsync(x => x.UserId == userId && x.TokenHash == tokenHash);

        if (token is null || token.ConsumedAt.HasValue || token.RevokedAt.HasValue)
        {
            return false;
        }

        if (token.ExpiresAt <= now)
        {
            token.RevokedAt = now;
            await _dbContext.SaveChangesAsync();
            return false;
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
        {
            return false;
        }

        user.IsEmailConfirmed = true;
        token.ConsumedAt = now;

        var remainingTokens = await _dbContext.EmailConfirmationTokens
            .Where(x => x.UserId == userId && x.Id != token.Id && x.ConsumedAt == null && x.RevokedAt == null)
            .ToListAsync();

        foreach (var remainingToken in remainingTokens)
        {
            remainingToken.RevokedAt = now;
        }

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Confirmed email for user {UserId}", userId);
        return true;
    }

    public async Task<bool> ConfirmEmailConfirmedAsync(string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        return await _dbContext.Users.AnyAsync(x => x.Email == normalizedEmail && x.IsEmailConfirmed);
    }

    public async Task<EmailTwoFactorChallengeDelivery?> CreateEmailTwoFactorChallengeAsync(Guid userId)
    {
        if (!_emailTwoFactorSettings.Enabled || userId == Guid.Empty)
        {
            return null;
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null || !user.IsActive || !user.IsEmailConfirmed || !user.IsTwoFactorEnabled)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var activeChallenges = await _dbContext.EmailTwoFactorChallenges
            .Where(x => x.UserId == userId && x.ConsumedAt == null && x.RevokedAt == null && x.ExpiresAt > now)
            .ToListAsync();

        foreach (var activeChallenge in activeChallenges)
        {
            activeChallenge.RevokedAt = now;
        }

        var code = GenerateNumericCode(_emailTwoFactorSettings.CodeLength);
        var challenge = new EmailTwoFactorChallenge
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CodeHash = HashToken(code),
            CreatedAt = now,
            LastSentAt = now,
            ExpiresAt = now.AddMinutes(_emailTwoFactorSettings.CodeExpiresInMinutes),
            FailedAttempts = 0
        };

        _dbContext.EmailTwoFactorChallenges.Add(challenge);
        await _dbContext.SaveChangesAsync();

        return new EmailTwoFactorChallengeDelivery(
            challenge.Id,
            user.Id,
            user.Email,
            user.DisplayName,
            code,
            challenge.ExpiresAt);
    }

    public async Task<User?> VerifyEmailTwoFactorChallengeAsync(Guid challengeId, string code)
    {
        if (!_emailTwoFactorSettings.Enabled || challengeId == Guid.Empty || string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var challenge = await _dbContext.EmailTwoFactorChallenges.FirstOrDefaultAsync(x => x.Id == challengeId);
        if (challenge is null || challenge.ConsumedAt.HasValue || challenge.RevokedAt.HasValue)
        {
            return null;
        }

        if (challenge.ExpiresAt <= now)
        {
            challenge.RevokedAt = now;
            await _dbContext.SaveChangesAsync();
            return null;
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == challenge.UserId);
        if (user is null || !user.IsActive || !user.IsEmailConfirmed || !user.IsTwoFactorEnabled)
        {
            return null;
        }

        if (!string.Equals(challenge.CodeHash, HashToken(code.Trim()), StringComparison.Ordinal))
        {
            challenge.FailedAttempts += 1;
            if (challenge.FailedAttempts >= _emailTwoFactorSettings.MaxFailedAttempts)
            {
                challenge.RevokedAt = now;
            }

            await _dbContext.SaveChangesAsync();
            return null;
        }

        challenge.ConsumedAt = now;

        var remainingChallenges = await _dbContext.EmailTwoFactorChallenges
            .Where(x => x.UserId == user.Id && x.Id != challenge.Id && x.ConsumedAt == null && x.RevokedAt == null)
            .ToListAsync();

        foreach (var remainingChallenge in remainingChallenges)
        {
            remainingChallenge.RevokedAt = now;
        }

        await _dbContext.SaveChangesAsync();
        return user;
    }

    public async Task<EmailTwoFactorChallengeDelivery?> ResendEmailTwoFactorChallengeAsync(Guid challengeId)
    {
        if (!_emailTwoFactorSettings.Enabled || challengeId == Guid.Empty)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var challenge = await _dbContext.EmailTwoFactorChallenges.FirstOrDefaultAsync(x => x.Id == challengeId);
        if (challenge is null || challenge.ConsumedAt.HasValue || challenge.RevokedAt.HasValue)
        {
            return null;
        }

        if (challenge.ExpiresAt <= now)
        {
            challenge.RevokedAt = now;
            await _dbContext.SaveChangesAsync();
            return null;
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == challenge.UserId);
        if (user is null || !user.IsActive || !user.IsEmailConfirmed || !user.IsTwoFactorEnabled)
        {
            return null;
        }

        var code = GenerateNumericCode(_emailTwoFactorSettings.CodeLength);
        challenge.CodeHash = HashToken(code);
        challenge.LastSentAt = now;
        challenge.ExpiresAt = now.AddMinutes(_emailTwoFactorSettings.CodeExpiresInMinutes);
        challenge.FailedAttempts = 0;

        await _dbContext.SaveChangesAsync();

        return new EmailTwoFactorChallengeDelivery(
            challenge.Id,
            user.Id,
            user.Email,
            user.DisplayName,
            code,
            challenge.ExpiresAt);
    }

    private static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static string GenerateNumericCode(int length)
    {
        var buffer = new char[length];

        for (var index = 0; index < length; index++)
        {
            buffer[index] = (char)('0' + RandomNumberGenerator.GetInt32(0, 10));
        }

        return new string(buffer);
    }

    private static string HashToken(string token)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hashBytes);
    }

    private static string NormalizeEmail(string email)
        => email.Trim().ToLowerInvariant();
}
