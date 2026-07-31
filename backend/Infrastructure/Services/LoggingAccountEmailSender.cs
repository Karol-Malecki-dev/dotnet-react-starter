using Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

/// <summary>
/// Local email sender used when external delivery is disabled.
/// It records confirmation links and 2FA codes through the application logger.
/// </summary>
public class LoggingAccountEmailSender : IAccountEmailSender
{
    private readonly ILogger<LoggingAccountEmailSender> _logger;

    public LoggingAccountEmailSender(ILogger<LoggingAccountEmailSender> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task SendEmailConfirmationAsync(string email, string displayName, string confirmationLink)
    {
        _logger.LogInformation(
            "Prepared email confirmation for {Email} ({DisplayName}). Confirmation link: {ConfirmationLink}",
            email,
            displayName,
            confirmationLink);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendPasswordResetLinkAsync(string email, string displayName, string resetLink)
    {
        _logger.LogInformation(
            "Prepared password reset email for {Email} ({DisplayName}). Reset link: {ResetLink}",
            email,
            displayName,
            resetLink);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendTwoFactorCodeAsync(string email, string displayName, string code, DateTime expiresAt)
    {
        _logger.LogInformation(
            "Prepared email 2FA code for {Email} ({DisplayName}). Code: {Code}. Expires at: {ExpiresAt:O}",
            email,
            displayName,
            code,
            expiresAt);

        return Task.CompletedTask;
    }
}