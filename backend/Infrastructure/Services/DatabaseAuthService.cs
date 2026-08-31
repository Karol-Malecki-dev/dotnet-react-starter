using Domain.Entities;
using Domain.Entities.Auth;
using Domain.Enums;
using Domain.Enums.Auth;
using Domain.Interfaces;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Settings;
using OtpNet;
using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Services;

/// <summary>
/// Database-backed authentication service responsible for account credentials,
/// email confirmation tokens, password reset requests, and email 2FA challenges.
/// </summary>
public class DatabaseAuthService : IAuthService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly EmailConfirmationSettings _emailConfirmationSettings;
    private readonly EmailTwoFactorSettings _emailTwoFactorSettings;
    private readonly AuthSecuritySettings _authSecuritySettings;
    private readonly ILogger<DatabaseAuthService> _logger;
    private readonly IDataProtector _authenticatorSecretProtector;
    private readonly PasswordHasher<User> _passwordHasher = new();

    public DatabaseAuthService(
        ApplicationDbContext dbContext,
        IOptions<EmailConfirmationSettings> emailConfirmationOptions,
        IOptions<EmailTwoFactorSettings> emailTwoFactorOptions,
        IOptions<AuthSecuritySettings> authSecurityOptions,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<DatabaseAuthService> logger)
    {
        _dbContext = dbContext;
        _emailConfirmationSettings = emailConfirmationOptions.Value;
        _emailTwoFactorSettings = emailTwoFactorOptions.Value;
        _authSecuritySettings = authSecurityOptions.Value;
        _authenticatorSecretProtector = dataProtectionProvider.CreateProtector("DatabaseAuthService.AuthenticatorSecret.v1");
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<User?> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        if (!EmailAddress.TryCreate(email, out var normalizedEmail) || normalizedEmail is null)
        {
            return null;
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);

        if (user is null || !user.IsActive)
        {
            _logger.LogWarning("Authentication failed for {Email}", normalizedEmail.Value);
            return null;
        }

        var now = DateTime.UtcNow;
        if (user.LockoutEndAt.HasValue && user.LockoutEndAt.Value > now)
        {
            _logger.LogWarning("Authentication blocked by lockout for user {UserId}", user.Id);
            return null;
        }

        var lockoutExpired = user.LockoutEndAt.HasValue && user.LockoutEndAt.Value <= now;
        if (lockoutExpired)
        {
            user.ResetLoginFailures();
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            var maxFailedLoginAttempts = Math.Max(1, _authSecuritySettings.MaxFailedLoginAttempts);
            var lockoutDuration = TimeSpan.FromMinutes(Math.Max(1, _authSecuritySettings.LockoutDurationMinutes));
            user.RecordFailedLogin(now, maxFailedLoginAttempts, lockoutDuration);
            await PersistAuthenticationStateAsync(user.Id, cancellationToken);
            _logger.LogWarning("Authentication failed for {Email}", normalizedEmail.Value);
            return null;
        }

        var authenticationStateChanged = lockoutExpired
            || user.FailedLoginAttempts > 0
            || user.LockoutEndAt.HasValue;

        if (authenticationStateChanged)
        {
            user.ResetLoginFailures();
        }

        if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.ChangePasswordHash(_passwordHasher.HashPassword(user, password));
            authenticationStateChanged = true;
        }

        if (authenticationStateChanged)
        {
            if (!await PersistAuthenticationStateAsync(user.Id, cancellationToken))
            {
                return null;
            }
        }

        return user;
    }

    public async Task<LoginAuditContext?> GetLoginAuditContextAsync(string email, CancellationToken cancellationToken = default)
    {
        if (!EmailAddress.TryCreate(email, out var normalizedEmail) || normalizedEmail is null)
        {
            return null;
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);

        return user is null
            ? null
            : new LoginAuditContext(user.Id, user.LockoutEndAt.HasValue && user.LockoutEndAt.Value > DateTime.UtcNow);
    }

    /// <inheritdoc />
    public async Task<User?> RegisterAsync(string email, string password, string displayName, CancellationToken cancellationToken = default)
    {
        if (!EmailAddress.TryCreate(email, out var normalizedEmail) || normalizedEmail is null)
        {
            return null;
        }

        if (!DisplayName.TryCreate(displayName, out var normalizedDisplayName) || normalizedDisplayName is null)
        {
            return null;
        }

        if (await _dbContext.Users.AnyAsync(x => x.Email == normalizedEmail, cancellationToken))
        {
            return null;
        }

        var user = User.Create(
            normalizedEmail,
            normalizedDisplayName,
            UserRole.User,
            isActive: true,
            isEmailConfirmed: false,
            isTwoFactorEnabled: _emailTwoFactorSettings.Enabled && _emailTwoFactorSettings.EnableForNewUsers);
        user.SetPasswordHash(_passwordHasher.HashPassword(user, password));

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Registered user {UserId} ({Email})", user.Id, user.Email.Value);
        return user;
    }

    /// <inheritdoc />
    public Task<bool> LogoutAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public async Task<bool> UserExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        if (!EmailAddress.TryCreate(email, out var normalizedEmail) || normalizedEmail is null)
        {
            return false;
        }

        return await _dbContext.Users.AnyAsync(x => x.Email == normalizedEmail, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> IsEmailConfirmedAsync(Guid userId, CancellationToken cancellationToken = default)
        => await _dbContext.Users.AnyAsync(x => x.Id == userId && x.IsEmailConfirmed, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> IsUserActiveAsync(Guid userId, CancellationToken cancellationToken = default)
        => await _dbContext.Users.AnyAsync(x => x.Id == userId && x.IsActive, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return false;
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return false;
        }

        user.ChangePasswordHash(_passwordHasher.HashPassword(user, newPassword));
        await RevokeActiveRefreshTokensAsync(user.Id, RevocationReason.PasswordChanged, DateTime.UtcNow, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> SendPasswordResetEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await GeneratePasswordResetTokenAsync(email, cancellationToken) is not null;
    }

    /// <inheritdoc />
    public async Task<string?> GeneratePasswordResetTokenAsync(string email, CancellationToken cancellationToken = default)
    {
        if (!EmailAddress.TryCreate(email, out var normalizedEmail) || normalizedEmail is null)
        {
            return null;
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var activeRequests = await _dbContext.PasswordResetRequests
            .Where(x => x.UserId == user.Id && x.ConsumedAt == null && x.RevokedAt == null && x.ExpiresAt > now)
            .ToListAsync(cancellationToken);

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
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Password reset request created for user {UserId} ({Email})", user.Id, user.Email.Value);
        return rawToken;
    }

    /// <inheritdoc />
    public async Task<bool> ResetPasswordAsync(string email, string resetToken, string newPassword, CancellationToken cancellationToken = default)
    {
        if (!EmailAddress.TryCreate(email, out var normalizedEmail) || normalizedEmail is null
            || string.IsNullOrWhiteSpace(resetToken) || string.IsNullOrWhiteSpace(newPassword))
        {
            return false;
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);
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
                x.RevokedAt == null, cancellationToken);

        if (resetRequest is null)
        {
            return false;
        }

        if (resetRequest.ExpiresAt <= now)
        {
            resetRequest.RevokedAt = now;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return false;
        }

        user.ChangePasswordHash(_passwordHasher.HashPassword(user, newPassword));
        resetRequest.ConsumedAt = now;

        var remainingRequests = await _dbContext.PasswordResetRequests
            .Where(x => x.UserId == user.Id && x.Id != resetRequest.Id && x.ConsumedAt == null && x.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var remainingRequest in remainingRequests)
        {
            remainingRequest.RevokedAt = now;
        }

        await RevokeActiveRefreshTokensAsync(user.Id, RevocationReason.PasswordReset, now, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<string?> GenerateEmailConfirmationTokenAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null || user.IsEmailConfirmed)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var activeTokens = await _dbContext.EmailConfirmationTokens
            .Where(x => x.UserId == userId && x.ConsumedAt == null && x.RevokedAt == null && x.ExpiresAt > now)
            .ToListAsync(cancellationToken);

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

        await _dbContext.SaveChangesAsync(cancellationToken);
        return rawToken;
    }

    /// <inheritdoc />
    public async Task<bool> ConfirmEmailAsync(Guid userId, string confirmationToken, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(confirmationToken))
        {
            return false;
        }

        var now = DateTime.UtcNow;
        var tokenHash = HashToken(confirmationToken);
        var token = await _dbContext.EmailConfirmationTokens
            .FirstOrDefaultAsync(x => x.UserId == userId && x.TokenHash == tokenHash, cancellationToken);

        if (token is null || token.ConsumedAt.HasValue || token.RevokedAt.HasValue)
        {
            return false;
        }

        if (token.ExpiresAt <= now)
        {
            token.RevokedAt = now;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return false;
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return false;
        }

        user.SetEmailConfirmed(true);
        token.ConsumedAt = now;

        var remainingTokens = await _dbContext.EmailConfirmationTokens
            .Where(x => x.UserId == userId && x.Id != token.Id && x.ConsumedAt == null && x.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var remainingToken in remainingTokens)
        {
            remainingToken.RevokedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Confirmed email for user {UserId}", userId);
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> ConfirmEmailConfirmedAsync(string email, CancellationToken cancellationToken = default)
    {
        if (!EmailAddress.TryCreate(email, out var normalizedEmail) || normalizedEmail is null)
        {
            return false;
        }

        return await _dbContext.Users.AnyAsync(x => x.Email == normalizedEmail && x.IsEmailConfirmed, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<EmailTwoFactorChallengeDelivery?> CreateEmailTwoFactorChallengeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (!_emailTwoFactorSettings.Enabled || userId == Guid.Empty)
        {
            return null;
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null || !user.IsActive || !user.IsEmailConfirmed || !user.IsTwoFactorEnabled)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var activeChallenges = await _dbContext.EmailTwoFactorChallenges
            .Where(x => x.UserId == userId && x.ConsumedAt == null && x.RevokedAt == null && x.ExpiresAt > now)
            .ToListAsync(cancellationToken);

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
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new EmailTwoFactorChallengeDelivery(
            challenge.Id,
            user.Id,
            user.Email.Value,
            user.DisplayName.Value,
            code,
            challenge.ExpiresAt);
    }

    /// <inheritdoc />
    public async Task<User?> VerifyEmailTwoFactorChallengeAsync(Guid challengeId, string code, CancellationToken cancellationToken = default)
    {
        if (!_emailTwoFactorSettings.Enabled || challengeId == Guid.Empty || string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var challenge = await _dbContext.EmailTwoFactorChallenges.FirstOrDefaultAsync(x => x.Id == challengeId, cancellationToken);
        if (challenge is null || challenge.ConsumedAt.HasValue || challenge.RevokedAt.HasValue)
        {
            return null;
        }

        if (challenge.ExpiresAt <= now)
        {
            challenge.RevokedAt = now;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == challenge.UserId, cancellationToken);
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

            await _dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }

        challenge.ConsumedAt = now;

        var remainingChallenges = await _dbContext.EmailTwoFactorChallenges
            .Where(x => x.UserId == user.Id && x.Id != challenge.Id && x.ConsumedAt == null && x.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var remainingChallenge in remainingChallenges)
        {
            remainingChallenge.RevokedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return user;
    }

    /// <inheritdoc />
    public async Task<EmailTwoFactorChallengeDelivery?> ResendEmailTwoFactorChallengeAsync(Guid challengeId, CancellationToken cancellationToken = default)
    {
        if (!_emailTwoFactorSettings.Enabled || challengeId == Guid.Empty)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var challenge = await _dbContext.EmailTwoFactorChallenges.FirstOrDefaultAsync(x => x.Id == challengeId, cancellationToken);
        if (challenge is null || challenge.ConsumedAt.HasValue || challenge.RevokedAt.HasValue)
        {
            return null;
        }

        if (challenge.ExpiresAt <= now)
        {
            challenge.RevokedAt = now;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == challenge.UserId, cancellationToken);
        if (user is null || !user.IsActive || !user.IsEmailConfirmed || !user.IsTwoFactorEnabled)
        {
            return null;
        }

        var code = GenerateNumericCode(_emailTwoFactorSettings.CodeLength);
        challenge.CodeHash = HashToken(code);
        challenge.LastSentAt = now;
        challenge.ExpiresAt = now.AddMinutes(_emailTwoFactorSettings.CodeExpiresInMinutes);
        challenge.FailedAttempts = 0;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new EmailTwoFactorChallengeDelivery(
            challenge.Id,
            user.Id,
            user.Email.Value,
            user.DisplayName.Value,
            code,
            challenge.ExpiresAt);
    }

    /// <inheritdoc />
    public async Task<AuthenticatorSetup?> BeginAuthenticatorSetupAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is null || !user.IsActive || !user.IsEmailConfirmed || user.IsAuthenticatorEnabled)
        {
            return null;
        }

        var sharedKey = Base32Encoding.ToString(RandomNumberGenerator.GetBytes(20));
        user.SetProtectedAuthenticatorSecret(_authenticatorSecretProtector.Protect(sharedKey));
        await _dbContext.SaveChangesAsync(cancellationToken);

        var issuer = "dotnet-react-starter";
        var label = Uri.EscapeDataString($"{issuer}:{user.Email.Value}");
        var provisioningUri = $"otpauth://totp/{label}?secret={sharedKey}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits=6&period=30";
        return new AuthenticatorSetup(sharedKey, provisioningUri);
    }

    /// <inheritdoc />
    public async Task<AuthenticatorConfirmation?> ConfirmAuthenticatorSetupAsync(Guid userId, string code, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is null || user.IsAuthenticatorEnabled || string.IsNullOrWhiteSpace(user.ProtectedAuthenticatorSecret)
            || !VerifyAuthenticatorCode(user.ProtectedAuthenticatorSecret, code))
        {
            return null;
        }

        var recoveryCodes = Enumerable.Range(0, 10).Select(_ => GenerateRecoveryCode()).ToArray();
        user.EnableAuthenticator();
        _dbContext.AuthenticatorRecoveryCodes.AddRange(recoveryCodes.Select(code => new AuthenticatorRecoveryCode
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CodeHash = HashToken(code),
            CreatedAt = DateTime.UtcNow
        }));
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new AuthenticatorConfirmation(recoveryCodes);
    }

    /// <inheritdoc />
    public async Task<AuthenticatorLoginChallengeInfo?> CreateAuthenticatorLoginChallengeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is null || !user.IsActive || !user.IsEmailConfirmed || !user.IsAuthenticatorEnabled)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var activeChallenges = await _dbContext.AuthenticatorLoginChallenges
            .Where(challenge => challenge.UserId == userId && challenge.ConsumedAt == null && challenge.ExpiresAt > now)
            .ToListAsync(cancellationToken);
        foreach (var activeChallenge in activeChallenges)
        {
            activeChallenge.ConsumedAt = now;
        }

        var challenge = new AuthenticatorLoginChallenge
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(5)
        };
        _dbContext.AuthenticatorLoginChallenges.Add(challenge);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new AuthenticatorLoginChallengeInfo(challenge.Id, challenge.ExpiresAt);
    }

    /// <inheritdoc />
    public async Task<TwoFactorVerificationResult?> VerifyAuthenticatorLoginChallengeAsync(Guid challengeId, string code, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var challenge = await _dbContext.AuthenticatorLoginChallenges.FirstOrDefaultAsync(candidate => candidate.Id == challengeId, cancellationToken);
        if (challenge is null || challenge.ConsumedAt.HasValue || challenge.ExpiresAt <= now)
        {
            return null;
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(candidate => candidate.Id == challenge.UserId, cancellationToken);
        if (user is null || !user.IsActive || !user.IsAuthenticatorEnabled || string.IsNullOrWhiteSpace(user.ProtectedAuthenticatorSecret))
        {
            return null;
        }

        var authenticatorCodeValid = VerifyAuthenticatorCode(user.ProtectedAuthenticatorSecret, code);
        var recoveryCodeUsed = !authenticatorCodeValid && await ConsumeRecoveryCodeAsync(user.Id, code, now, cancellationToken);
        var isValid = authenticatorCodeValid || recoveryCodeUsed;
        if (!isValid)
        {
            return null;
        }

        challenge.ConsumedAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new TwoFactorVerificationResult(user, recoveryCodeUsed);
    }

    /// <inheritdoc />
    public async Task<bool> DisableAuthenticatorAsync(Guid userId, string currentPassword, string code, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is null || !user.IsAuthenticatorEnabled || string.IsNullOrWhiteSpace(user.ProtectedAuthenticatorSecret))
        {
            return false;
        }

        var now = DateTime.UtcNow;
        var isValid = VerifyCurrentPassword(user, currentPassword)
            && (VerifyAuthenticatorCode(user.ProtectedAuthenticatorSecret, code) || await ConsumeRecoveryCodeAsync(user.Id, code, now, cancellationToken));
        if (!isValid)
        {
            return false;
        }

        user.DisableAuthenticator();
        var recoveryCodes = await _dbContext.AuthenticatorRecoveryCodes.Where(recoveryCode => recoveryCode.UserId == userId).ToListAsync(cancellationToken);
        _dbContext.AuthenticatorRecoveryCodes.RemoveRange(recoveryCodes);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public async Task<AuthenticatorConfirmation?> RegenerateAuthenticatorRecoveryCodesAsync(Guid userId, string currentPassword, string code, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is null || !user.IsAuthenticatorEnabled || string.IsNullOrWhiteSpace(user.ProtectedAuthenticatorSecret))
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var isValid = VerifyCurrentPassword(user, currentPassword)
            && (VerifyAuthenticatorCode(user.ProtectedAuthenticatorSecret, code) || await ConsumeRecoveryCodeAsync(user.Id, code, now, cancellationToken));
        if (!isValid)
        {
            return null;
        }

        var existingRecoveryCodes = await _dbContext.AuthenticatorRecoveryCodes.Where(recoveryCode => recoveryCode.UserId == userId).ToListAsync(cancellationToken);
        _dbContext.AuthenticatorRecoveryCodes.RemoveRange(existingRecoveryCodes);
        var recoveryCodes = Enumerable.Range(0, 10).Select(_ => GenerateRecoveryCode()).ToArray();
        _dbContext.AuthenticatorRecoveryCodes.AddRange(recoveryCodes.Select(recoveryCode => new AuthenticatorRecoveryCode
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CodeHash = HashToken(recoveryCode),
            CreatedAt = now
        }));
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new AuthenticatorConfirmation(recoveryCodes);
    }

    private bool VerifyAuthenticatorCode(string protectedSecret, string code)
    {
        try
        {
            var secret = _authenticatorSecretProtector.Unprotect(protectedSecret);
            var totp = new Totp(Base32Encoding.ToBytes(secret));
            return totp.VerifyTotp(code.Trim(), out _, new VerificationWindow(previous: 1, future: 1));
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException)
        {
            _logger.LogWarning(exception, "Could not unprotect or parse an authenticator secret");
            return false;
        }
    }

    private async Task<bool> ConsumeRecoveryCodeAsync(Guid userId, string code, DateTime now, CancellationToken cancellationToken)
    {
        var codeHash = HashToken(code.Trim());
        var recoveryCode = await _dbContext.AuthenticatorRecoveryCodes
            .FirstOrDefaultAsync(candidate => candidate.UserId == userId && candidate.CodeHash == codeHash && candidate.UsedAt == null, cancellationToken);
        if (recoveryCode is null)
        {
            return false;
        }

        recoveryCode.UsedAt = now;
        return true;
    }

    private bool VerifyCurrentPassword(User user, string currentPassword)
    {
        if (string.IsNullOrWhiteSpace(currentPassword))
        {
            return false;
        }

        return _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword) != PasswordVerificationResult.Failed;
    }

    private async Task<bool> PersistAuthenticationStateAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            _dbContext.ChangeTracker.Clear();
            _logger.LogWarning("Authentication state changed concurrently for user {UserId}", userId);
            return false;
        }
    }

    private async Task RevokeActiveRefreshTokensAsync(Guid userId, RevocationReason reason, DateTime revokedAt, CancellationToken cancellationToken)
    {
        var activeTokens = await _dbContext.RefreshTokens
            .Where(token => token.UserId == userId && !token.RevokedAt.HasValue)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.RevokedAt = revokedAt;
            token.RevocationReason = reason;
            token.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        }
    }


    private static string GenerateRecoveryCode()
        => Convert.ToHexString(RandomNumberGenerator.GetBytes(8));

    /// <summary>
    /// Generates a cryptographically secure URL-safe token.
    /// </summary>
    /// <returns>A raw token that must not be logged or persisted as plain text.</returns>
    private static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <summary>
    /// Generates a cryptographically secure numeric verification code.
    /// </summary>
    /// <param name="length">Number of digits to generate.</param>
    /// <returns>A numeric code with the requested length.</returns>
    private static string GenerateNumericCode(int length)
    {
        var buffer = new char[length];

        for (var index = 0; index < length; index++)
        {
            buffer[index] = (char)('0' + RandomNumberGenerator.GetInt32(0, 10));
        }

        return new string(buffer);
    }

    /// <summary>
    /// Creates the SHA-256 hash used for comparing one-time secrets with persisted values.
    /// </summary>
    /// <param name="token">Raw token or code to hash.</param>
    /// <returns>Uppercase hexadecimal SHA-256 hash.</returns>
    private static string HashToken(string token)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hashBytes);
    }

}
