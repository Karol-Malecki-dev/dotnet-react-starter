using Domain.Entities;
using Domain.Enums.Auth;
using Domain.ValueObjects;

namespace Domain.Interfaces
{
    public interface IAuthService
    {
        // ========== AUTHENTICATION OPERATIONS ==========

        /// <summary>
        /// Authenticate user with email and password.
        /// Returns User entity if authentication successful.
        /// </summary>
        /// <param name="email">The user email</param>
        /// <param name="password">The user password</param>
        /// <returns>User entity or null if authentication failed</returns>
        Task<User?> AuthenticateAsync(string email, string password);

        /// <summary>
        /// Register a new user account.
        /// Creates user and returns the created user entity.
        /// </summary>
        /// <param name="email">The user email</param>
        /// <param name="password">The user password</param>
        /// <param name="displayName">The user display name</param>
        /// <returns>Created user entity or null if registration failed</returns>
        Task<User?> RegisterAsync(string email, string password, string displayName);

        /// <summary>
        /// Logout user (invalidate tokens).
        /// </summary>
        /// <param name="userId">The user ID to logout</param>
        /// <returns>Result indicating success or failure</returns>
        Task<bool> LogoutAsync(Guid userId);

        // ========== USER VERIFICATION ==========

        /// <summary>
        /// Verify if user email exists in system.
        /// </summary>
        /// <param name="email">The email to check</param>
        /// <returns>True if user exists, false otherwise</returns>
        Task<bool> UserExistsAsync(string email);

        /// <summary>
        /// Verify if user email is confirmed.
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <returns>True if email is confirmed, false otherwise</returns>
        Task<bool> IsEmailConfirmedAsync(Guid userId);

        /// <summary>
        /// Check if user account is active.
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <returns>True if user is active, false otherwise</returns>
        Task<bool> IsUserActiveAsync(Guid userId);

        // ========== PASSWORD OPERATIONS ==========

        /// <summary>
        /// Change user password (requires current password verification).
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <param name="currentPassword">The current password</param>
        /// <param name="newPassword">The new password</param>
        /// <returns>True if password changed successfully, false otherwise</returns>
        Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);

        /// <summary>
        /// Trigger the forgot-password flow for a known email address.
        /// </summary>
        /// <param name="email">The user email</param>
        /// <returns>True when the email exists and the reset flow can continue</returns>
        Task<bool> SendPasswordResetEmailAsync(string email);

        /// <summary>
        /// Reset the user password by using a previously issued reset token.
        /// </summary>
        /// <param name="email">The user email.</param>
        /// <param name="resetToken">The raw reset token received by the user.</param>
        /// <param name="newPassword">The new plain text password to hash on the server.</param>
        /// <returns>True when the password was reset successfully; otherwise false.</returns>
        Task<bool> ResetPasswordAsync(string email, string resetToken, string newPassword);

        // ========== EMAIL VERIFICATION ==========


        /// <summary>
        /// Generate email confirmation token.
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <returns>Confirmation token string or null if user not found</returns>
        Task<string?> GenerateEmailConfirmationTokenAsync(Guid userId);

        /// <summary>
        /// Confirm user email with verification token.
        /// </summary>
        /// <param name="userId">The user ID</param>
        /// <param name="confirmationToken">The confirmation token</param>
        /// <returns>True if email confirmed successfully, false otherwise</returns>
        Task<bool> ConfirmEmailAsync(Guid userId, string confirmationToken);

        Task<bool> ConfirmEmailConfirmedAsync(string email);

        /// <summary>
        /// Create a new email 2FA challenge for a user.
        /// </summary>
        /// <param name="userId">The user ID.</param>
        /// <returns>Delivery payload with raw code when challenge was created; otherwise null.</returns>
        Task<EmailTwoFactorChallengeDelivery?> CreateEmailTwoFactorChallengeAsync(Guid userId);

        /// <summary>
        /// Verify a previously issued email 2FA challenge.
        /// </summary>
        /// <param name="challengeId">The challenge identifier.</param>
        /// <param name="code">The raw user-provided code.</param>
        /// <returns>The authenticated user when the code is valid; otherwise null.</returns>
        Task<User?> VerifyEmailTwoFactorChallengeAsync(Guid challengeId, string code);

        /// <summary>
        /// Rotate and resend the code for an active email 2FA challenge.
        /// </summary>
        /// <param name="challengeId">The active challenge identifier.</param>
        /// <returns>Delivery payload with the new raw code when the challenge is active; otherwise null.</returns>
        Task<EmailTwoFactorChallengeDelivery?> ResendEmailTwoFactorChallengeAsync(Guid challengeId);
    }
}
