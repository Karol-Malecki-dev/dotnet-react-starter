using Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class LoggingAccountEmailSender : IAccountEmailSender
{
    private readonly ILogger<LoggingAccountEmailSender> _logger;

    public LoggingAccountEmailSender(ILogger<LoggingAccountEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendEmailConfirmationAsync(string email, string displayName, string confirmationLink)
    {
        _logger.LogInformation(
            "Prepared email confirmation for {Email} ({DisplayName}). Confirmation link: {ConfirmationLink}",
            email,
            displayName,
            confirmationLink);

        return Task.CompletedTask;
    }

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