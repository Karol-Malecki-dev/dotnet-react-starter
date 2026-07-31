using Domain.Entities;
using Domain.Enums.Auth;
using Domain.ValueObjects;

namespace Domain.Interfaces
{
    /// <summary>
    /// Provides authentication, account verification, password management,
    /// and email-based two-factor authentication operations.
    /// </summary>
    public interface IAuthService
    {
        // ========== AUTHENTICATION OPERATIONS ==========

        /// <summary>
        /// Authenticates an active user by verifying the supplied password.
        /// </summary>
        /// <param name="email">Email address used to locate the account. It is normalized before querying the database.</param>
        /// <param name="password">Plain-text password used for verification. It is never persisted as plain text.</param>
        /// <returns>The authenticated user, or <see langword="null"/> when the account is missing, inactive, or the password is invalid.</returns>
        Task<User?> AuthenticateAsync(string email, string password);

        /// <summary>
        /// Creates a new user account with a hashed password.
        /// The account starts without a confirmed email address.
        /// </summary>
        /// <param name="email">Email address used as the unique account identifier.</param>
        /// <param name="password">Plain-text password that is hashed before persistence.</param>
        /// <param name="displayName">Display name assigned to the new account.</param>
        /// <returns>The created user, or <see langword="null"/> when the email is already registered.</returns>
        Task<User?> RegisterAsync(string email, string password, string displayName);

        /// <summary>
        /// Performs the authentication-service logout operation for a user.
        /// </summary>
        /// <param name="userId">Identifier of the user whose session is being terminated.</param>
        /// <returns><see langword="true"/> when the operation succeeds; otherwise, <see langword="false"/>.</returns>
        /// <remarks>Refresh-token revocation and cookie cleanup are handled by the token/controller layer.</remarks>
        Task<bool> LogoutAsync(Guid userId);

        // ========== USER VERIFICATION ==========

        /// <summary>
        /// Determines whether an account exists for the supplied email address.
        /// </summary>
        /// <param name="email">Email address to normalize and look up.</param>
        /// <returns><see langword="true"/> when an account exists; otherwise, <see langword="false"/>.</returns>
        Task<bool> UserExistsAsync(string email);

        /// <summary>
        /// Determines whether a user's email address has been confirmed.
        /// </summary>
        /// <param name="userId">Identifier of the user to check.</param>
        /// <returns><see langword="true"/> when the email is confirmed; otherwise, <see langword="false"/>.</returns>
        Task<bool> IsEmailConfirmedAsync(Guid userId);

        /// <summary>
        /// Determines whether a user account is active.
        /// </summary>
        /// <param name="userId">Identifier of the user to check.</param>
        /// <returns><see langword="true"/> when the account is active; otherwise, <see langword="false"/>.</returns>
        Task<bool> IsUserActiveAsync(Guid userId);

        // ========== PASSWORD OPERATIONS ==========

        /// <summary>
        /// Changes a user's password after verifying the current password.
        /// </summary>
        /// <param name="userId">Identifier of the user whose password is being changed.</param>
        /// <param name="currentPassword">Current plain-text password used for verification.</param>
        /// <param name="newPassword">New plain-text password that is hashed before persistence.</param>
        /// <returns><see langword="true"/> when the password was changed; otherwise, <see langword="false"/>.</returns>
        Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);

        /// <summary>
        /// Creates a password reset request for an existing user.
        /// </summary>
        /// <param name="email">Email address associated with the account.</param>
        /// <returns><see langword="true"/> when a reset request was created; otherwise, <see langword="false"/>.</returns>
        /// <remarks>
        /// Previously active reset requests are revoked and only the hash of the new token is persisted.
        /// Email delivery is handled by the calling layer. Public endpoints should use a neutral response
        /// to avoid revealing whether an account exists.
        /// </remarks>
        Task<bool> SendPasswordResetEmailAsync(string email);

        /// <summary>Creates a single-use password reset request and returns its raw token for email delivery.</summary>
        Task<string?> GeneratePasswordResetTokenAsync(string email);

        /// <summary>
        /// Resets a user's password by consuming a previously issued link token.
        /// </summary>
        /// <param name="email">Email address associated with the reset request.</param>
        /// <param name="resetToken">Raw single-use token received by the user.</param>
        /// <param name="newPassword">New plain-text password that is hashed before persistence.</param>
        /// <returns><see langword="true"/> when the password was reset; otherwise, <see langword="false"/>.</returns>
        /// <remarks>Expired, revoked, consumed, or unknown tokens are rejected.</remarks>
        Task<bool> ResetPasswordAsync(string email, string resetToken, string newPassword);

        // ========== EMAIL VERIFICATION ==========


        /// <summary>
        /// Generates a single-use email confirmation token for an unconfirmed user.
        /// </summary>
        /// <param name="userId">Identifier of the account to confirm.</param>
        /// <returns>The raw token for an email link, or <see langword="null"/> when the user is missing or already confirmed.</returns>
        /// <remarks>Previously active confirmation tokens are revoked and only the new token hash is persisted.</remarks>
        Task<string?> GenerateEmailConfirmationTokenAsync(Guid userId);

        /// <summary>
        /// Consumes an email confirmation token and marks the user's email as confirmed.
        /// </summary>
        /// <param name="userId">Identifier of the account being confirmed.</param>
        /// <param name="confirmationToken">Raw single-use token received from the confirmation link.</param>
        /// <returns><see langword="true"/> when the email was confirmed; otherwise, <see langword="false"/>.</returns>
        /// <remarks>Expired, revoked, consumed, or unknown tokens are rejected.</remarks>
        Task<bool> ConfirmEmailAsync(Guid userId, string confirmationToken);

        /// <summary>
        /// Determines whether an account identified by email has a confirmed email address.
        /// </summary>
        /// <param name="email">Email address to normalize and look up.</param>
        /// <returns><see langword="true"/> when the email is confirmed; otherwise, <see langword="false"/>.</returns>
        Task<bool> ConfirmEmailConfirmedAsync(string email);

        /// <summary>
        /// Creates a short-lived email-based two-factor challenge for a user.
        /// </summary>
        /// <param name="userId">Identifier of the user completing the sign-in flow.</param>
        /// <returns>A delivery payload with the raw code, or <see langword="null"/> when the challenge cannot be created.</returns>
        /// <remarks>
        /// A challenge is created only when email 2FA is enabled and the user is active, email-confirmed,
        /// and configured to use two-factor authentication. Only the code hash is persisted.
        /// </remarks>
        Task<EmailTwoFactorChallengeDelivery?> CreateEmailTwoFactorChallengeAsync(Guid userId);

        /// <summary>
        /// Verifies a previously issued email-based two-factor challenge.
        /// </summary>
        /// <param name="challengeId">Identifier of the pending challenge.</param>
        /// <param name="code">Raw code entered by the user.</param>
        /// <returns>The authenticated user when the code is valid; otherwise, <see langword="null"/>.</returns>
        /// <remarks>Failed attempts are counted and the challenge is revoked after the configured limit.</remarks>
        Task<User?> VerifyEmailTwoFactorChallengeAsync(Guid challengeId, string code);

        /// <summary>
        /// Rotates and resends the code for an active email two-factor challenge.
        /// </summary>
        /// <param name="challengeId">Identifier of the active challenge.</param>
        /// <returns>A delivery payload with the new raw code, or <see langword="null"/> when the challenge is invalid.</returns>
        /// <remarks>The expiration time and failed-attempt counter are reset for the newly sent code.</remarks>
        Task<EmailTwoFactorChallengeDelivery?> ResendEmailTwoFactorChallengeAsync(Guid challengeId);

        /// <summary>Creates or replaces a pending authenticator-app setup for a confirmed account.</summary>
        Task<AuthenticatorSetup?> BeginAuthenticatorSetupAsync(Guid userId);

        /// <summary>Confirms a pending authenticator setup and returns single-use recovery codes.</summary>
        Task<AuthenticatorConfirmation?> ConfirmAuthenticatorSetupAsync(Guid userId, string code);

        /// <summary>Creates a short-lived challenge after password validation for an authenticator-app sign-in.</summary>
        Task<AuthenticatorLoginChallengeInfo?> CreateAuthenticatorLoginChallengeAsync(Guid userId);

        /// <summary>Completes an authenticator-app sign-in using a current TOTP or recovery code.</summary>
        Task<User?> VerifyAuthenticatorLoginChallengeAsync(Guid challengeId, string code);

        /// <summary>Disables an authenticator application after re-authenticating with password and a current TOTP or recovery code.</summary>
        Task<bool> DisableAuthenticatorAsync(Guid userId, string currentPassword, string code);

        /// <summary>Replaces all recovery codes after re-authenticating with password and a current TOTP or recovery code.</summary>
        Task<AuthenticatorConfirmation?> RegenerateAuthenticatorRecoveryCodesAsync(Guid userId, string currentPassword, string code);
    }
}
