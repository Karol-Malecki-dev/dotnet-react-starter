using Application.DTOs.User;
using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Shared.Responses;
using System;
using System.Threading.Tasks;

namespace Application.Services
{
    /// <summary>
    /// Mock Authentication Service - for testing JWT without database
    /// Replace with real implementation later
    /// </summary>
    public class MockAuthService : IAuthService
    {
        private readonly ILogger<MockAuthService> _logger;

        // Hardcoded test users
        private static readonly User TestUser = new()
        {
            Id = Guid.Parse("550e8400-e29b-41d4-a716-446655440000"),
            Email = "test@example.com",
            DisplayName = "Test User",
            Role = Domain.Enums.UserRole.User,
            IsActive = true,
            IsEmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
            PasswordHash = "hashed_password_123" // In real app, use BCrypt
        };

        public MockAuthService(ILogger<MockAuthService> logger)
        {
            _logger = logger;
        }

        public async Task<User?> AuthenticateAsync(string email, string password)
        {
            _logger.LogInformation("🔐 Mock authentication attempt: {Email}", email);

            // Mock: accept any password for test@example.com
            if (email == TestUser.Email && password == "password123")
            {
                _logger.LogInformation("✓ Mock authentication successful");
                return await Task.FromResult(TestUser);
            }

            _logger.LogWarning("⚠️ Mock authentication failed for {Email}", email);
            return await Task.FromResult<User?>(null);
        }

        public async Task<User?> RegisterAsync(string email, string password, string displayName)
        {
            _logger.LogInformation("📝 Mock registration: {Email}", email);

            // Mock: always accept registration
            var newUser = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                DisplayName = displayName,
                Role = Domain.Enums.UserRole.User,
                IsActive = true,
                IsEmailConfirmed = false,
                CreatedAt = DateTime.UtcNow,
                PasswordHash = "hashed_password_" + Guid.NewGuid().ToString().Substring(0, 8)
            };

            _logger.LogInformation("✓ Mock registration successful for user {UserId}", newUser.Id);
            return await Task.FromResult(newUser);
        }

        public async Task<bool> LogoutAsync(Guid userId)
        {
            _logger.LogInformation("🚪 Mock logout for user {UserId}", userId);
            return await Task.FromResult(true);
        }

        public async Task<bool> UserExistsAsync(string email)
        {
            return await Task.FromResult(email == TestUser.Email);
        }

        public async Task<bool> IsEmailConfirmedAsync(Guid userId)
        {
            return await Task.FromResult(userId == TestUser.Id);
        }

        public async Task<bool> IsUserActiveAsync(Guid userId)
        {
            return await Task.FromResult(userId == TestUser.Id);
        }

        public async Task<string?> GeneratePasswordResetTokenAsync(string email)
        {
            return await Task.FromResult(email == TestUser.Email ? Guid.NewGuid().ToString() : null);
        }

        public async Task<bool> ResetPasswordAsync(string email, string resetToken, string newPassword)
        {
            _logger.LogInformation("🔄 Mock password reset for {Email}", email);
            return await Task.FromResult(true);
        }

        public async Task<string?> GenerateEmailConfirmationTokenAsync(Guid userId)
        {
            return await Task.FromResult(Guid.NewGuid().ToString());
        }

        public async Task<bool> ConfirmEmailAsync(Guid userId, string confirmationToken)
        {
            _logger.LogInformation("✉️ Mock email confirmation for user {UserId}", userId);
            return await Task.FromResult(true);
        }

        public async Task<bool> ConfirmEmailConfirmedAsync(string email)
        {
            _logger.LogInformation("✉️ Mock email confirmation check for {Email}", email);
            return await Task.FromResult(email == TestUser.Email);
        }

        public Task<EmailTwoFactorChallengeDelivery?> CreateEmailTwoFactorChallengeAsync(Guid userId)
        {
            if (userId != TestUser.Id)
            {
                return Task.FromResult<EmailTwoFactorChallengeDelivery?>(null);
            }

            return Task.FromResult<EmailTwoFactorChallengeDelivery?>(new EmailTwoFactorChallengeDelivery(
                Guid.NewGuid(),
                TestUser.Id,
                TestUser.Email,
                TestUser.DisplayName,
                "123456",
                DateTime.UtcNow.AddMinutes(10)));
        }

        public Task<User?> VerifyEmailTwoFactorChallengeAsync(Guid challengeId, string code)
        {
            return Task.FromResult<User?>(code == "123456" ? TestUser : null);
        }

        public Task<EmailTwoFactorChallengeDelivery?> ResendEmailTwoFactorChallengeAsync(Guid challengeId)
        {
            if (challengeId == Guid.Empty)
            {
                return Task.FromResult<EmailTwoFactorChallengeDelivery?>(null);
            }

            return Task.FromResult<EmailTwoFactorChallengeDelivery?>(new EmailTwoFactorChallengeDelivery(
                challengeId,
                TestUser.Id,
                TestUser.Email,
                TestUser.DisplayName,
                "654321",
                DateTime.UtcNow.AddMinutes(10)));
        }

        public Task<AuthenticatorSetup?> BeginAuthenticatorSetupAsync(Guid userId)
            => Task.FromResult<AuthenticatorSetup?>(userId == TestUser.Id
                ? new AuthenticatorSetup("JBSWY3DPEHPK3PXP", "otpauth://totp/dotnet-react-starter:test%40example.com?secret=JBSWY3DPEHPK3PXP")
                : null);

        public Task<AuthenticatorConfirmation?> ConfirmAuthenticatorSetupAsync(Guid userId, string code)
            => Task.FromResult<AuthenticatorConfirmation?>(userId == TestUser.Id && code == "123456"
                ? new AuthenticatorConfirmation(["AAAA-BBBB-CCCC-DDDD"])
                : null);

        public Task<AuthenticatorLoginChallengeInfo?> CreateAuthenticatorLoginChallengeAsync(Guid userId)
            => Task.FromResult<AuthenticatorLoginChallengeInfo?>(userId == TestUser.Id
                ? new AuthenticatorLoginChallengeInfo(Guid.NewGuid(), DateTime.UtcNow.AddMinutes(5))
                : null);

        public Task<User?> VerifyAuthenticatorLoginChallengeAsync(Guid challengeId, string code)
            => Task.FromResult<User?>(challengeId != Guid.Empty && code == "123456" ? TestUser : null);

        public Task<bool> DisableAuthenticatorAsync(Guid userId, string currentPassword, string code)
            => Task.FromResult(userId == TestUser.Id && currentPassword == "password123" && code == "123456");

        public Task<AuthenticatorConfirmation?> RegenerateAuthenticatorRecoveryCodesAsync(Guid userId, string currentPassword, string code)
            => Task.FromResult<AuthenticatorConfirmation?>(userId == TestUser.Id && currentPassword == "password123" && code == "123456"
                ? new AuthenticatorConfirmation(["EEEE-FFFF-GGGG-HHHH"])
                : null);

        public async Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
        {
            _logger.LogInformation("🔑 Mock change password for user {UserId}", userId);
            if (userId != TestUser.Id)
            {
                return await Task.FromResult(false);
            }

            if (currentPassword != "password123")
            {
                return await Task.FromResult(false);
            }
            return await Task.FromResult(true);
        }

        public async Task<bool> SendPasswordResetEmailAsync(string email)
        {
            _logger.LogInformation("📨 Mock forgot password for {Email}", email);
            return await Task.FromResult(email == TestUser.Email);
        }

        public async Task<bool> UpdateUserEmail(Guid userId, string newEmail)
        {
            _logger.LogInformation("📧 Mock update email for user {UserId} to {NewEmail}", userId, newEmail);
            
            if(userId != TestUser.Id)
            {
                return await Task.FromResult(false);
            }
            if(newEmail != TestUser.Email)
            {
                return await Task.FromResult(false);
            }
            return await Task.FromResult(true);
        }
        public async Task<bool> UpdateUserPasswordHash(Guid userId, string newEmail)
        {
            _logger.LogInformation("📧 Mock update email for user {UserId} to {NewEmail}", userId, newEmail);

            if (userId != TestUser.Id)
            {
                return await Task.FromResult(false);
            }
            if (newEmail != TestUser.Email)
            {
                return await Task.FromResult(false);
            }
            return await Task.FromResult(true);
        }
        public async Task<bool> UpdateUserDisplayName(Guid userId, string newEmail)
        {
            _logger.LogInformation("📧 Mock update email for user {UserId} to {NewEmail}", userId, newEmail);

            if (userId != TestUser.Id)
            {
                return await Task.FromResult(false);
            }
            if (newEmail != TestUser.Email)
            {
                return await Task.FromResult(false);
            }
            return await Task.FromResult(true);
        }
        public async Task<bool> UpdateUserAvatarUrl(Guid userId, string newEmail)
        {
            _logger.LogInformation("📧 Mock update email for user {UserId} to {NewEmail}", userId, newEmail);

            if (userId != TestUser.Id)
            {
                return await Task.FromResult(false);
            }
            if (newEmail != TestUser.Email)
            {
                return await Task.FromResult(false);
            }
            return await Task.FromResult(true);
        }
        public async Task<bool> UpdateUserRole(Guid userId, string newEmail)
        {
            _logger.LogInformation("📧 Mock update email for user {UserId} to {NewEmail}", userId, newEmail);

            if (userId != TestUser.Id)
            {
                return await Task.FromResult(false);
            }
            if (newEmail != TestUser.Email)
            {
                return await Task.FromResult(false);
            }
            return await Task.FromResult(true);
        }
    }
}