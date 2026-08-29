using Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public sealed class LoggingNotificationEmailSender : INotificationEmailSender
{
    private readonly ILogger<LoggingNotificationEmailSender> _logger;

    public LoggingNotificationEmailSender(ILogger<LoggingNotificationEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string email, string displayName, string title, string message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Prepared notification email '{Title}' for {Email} ({DisplayName}): {Message}", title, email, displayName, message);
        return Task.CompletedTask;
    }
}
