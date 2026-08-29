using Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Shared.Settings;

namespace Infrastructure.Services;

public sealed class MailKitNotificationEmailSender : INotificationEmailSender
{
    private readonly EmailDeliverySettings _settings;
    private readonly ILogger<MailKitNotificationEmailSender> _logger;

    public MailKitNotificationEmailSender(
        IOptions<EmailDeliverySettings> settings,
        ILogger<MailKitNotificationEmailSender> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendAsync(string email, string displayName, string title, string message, CancellationToken cancellationToken = default)
    {
        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
        mimeMessage.To.Add(new MailboxAddress(displayName, email));
        mimeMessage.Subject = title;
        mimeMessage.Body = new BodyBuilder
        {
            HtmlBody = $"<p>Hello {Escape(displayName)},</p><p>{Escape(message)}</p>",
            TextBody = $"Hello {displayName},\n\n{message}"
        }.ToMessageBody();

        using var client = new SmtpClient();
        var socketOptions = _settings.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
        await client.ConnectAsync(_settings.Host, _settings.Port, socketOptions, cancellationToken);
        if (!string.IsNullOrWhiteSpace(_settings.Username))
        {
            await client.AuthenticateAsync(_settings.Username, _settings.Password ?? string.Empty, cancellationToken);
        }

        await client.SendAsync(mimeMessage, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
        _logger.LogInformation("Sent notification email '{Title}' to {Email}", title, email);
    }

    private static string Escape(string value) => System.Net.WebUtility.HtmlEncode(value);
}
