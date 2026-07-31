namespace Application.Interfaces;

/// <summary>Abstraction for account-related emails used by authentication flows.</summary>
public interface IAccountEmailSender
{
    /// <summary>Sends the single-use email confirmation link for a newly registered account.</summary>
    /// <param name="email">Recipient email address.</param>
    /// <param name="displayName">Recipient display name used in the message.</param>
    /// <param name="confirmationLink">Frontend link containing the confirmation token.</param>
    Task SendEmailConfirmationAsync(string email, string displayName, string confirmationLink);

    /// <summary>Sends a single-use password reset link.</summary>
    Task SendPasswordResetLinkAsync(string email, string displayName, string resetLink);

    /// <summary>Sends the short-lived email 2FA code for a pending login challenge.</summary>
    /// <param name="email">Recipient email address.</param>
    /// <param name="displayName">Recipient display name used in the message.</param>
    /// <param name="code">Short-lived verification code.</param>
    /// <param name="expiresAt">UTC time when the code expires.</param>
    Task SendTwoFactorCodeAsync(string email, string displayName, string code, DateTime expiresAt);
}