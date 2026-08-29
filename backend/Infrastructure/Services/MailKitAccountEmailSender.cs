using Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Shared.Settings;

namespace Infrastructure.Services;

/// <summary>
/// SMTP-backed account email sender implemented with MailKit.
/// It is registered when email delivery is enabled in runtime configuration.
/// </summary>
public class MailKitAccountEmailSender : IAccountEmailSender
{
    private readonly EmailDeliverySettings _settings;
    private readonly ILogger<MailKitAccountEmailSender> _logger;

    public MailKitAccountEmailSender(
        IOptions<EmailDeliverySettings> settings,
        ILogger<MailKitAccountEmailSender> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task SendEmailConfirmationAsync(string email, string displayName, string confirmationLink, CancellationToken cancellationToken = default)
    {
        var subject = "Confirm your email address";
        var htmlBody = $"""
            <p>Hello {Escape(displayName)},</p>
            <p>Confirm your account by clicking the link below:</p>
            <p><a href=\"{confirmationLink}\">Confirm email address</a></p>
            <p>If you did not create this account, you can ignore this email.</p>
            """;
        var textBody = $"Hello {displayName},\n\nConfirm your account by visiting: {confirmationLink}\n\nIf you did not create this account, you can ignore this email.";

        return SendAsync(email, displayName, subject, htmlBody, textBody, cancellationToken);
    }

    /// <inheritdoc />
    public Task SendPasswordResetLinkAsync(string email, string displayName, string resetLink, CancellationToken cancellationToken = default)
    {
        var subject = "Reset your password";
        var htmlBody = $"""
            <p>Hello {Escape(displayName)},</p>
            <p>Reset your password by clicking the link below:</p>
            <p><a href=\"{resetLink}\">Reset password</a></p>
            <p>If you did not request this, you can ignore this email.</p>
            """;
        var textBody = $"Hello {displayName},\n\nReset your password by visiting: {resetLink}\n\nIf you did not request this, you can ignore this email.";

        return SendAsync(email, displayName, subject, htmlBody, textBody, cancellationToken);
    }

    /// <inheritdoc />
    public Task SendTwoFactorCodeAsync(string email, string displayName, string code, DateTime expiresAt, CancellationToken cancellationToken = default)
    {
        var subject = "Your verification code";
        var htmlBody = $"""
            <p>Hello {Escape(displayName)},</p>
            <p>Your email verification code is:</p>
            <p style=\"font-size: 1.5rem; font-weight: bold; letter-spacing: 0.2rem;\">{Escape(code)}</p>
            <p>The code expires at {expiresAt:u}.</p>
            <p>If you did not attempt to sign in, reset your password and review account activity.</p>
            """;
        var textBody = $"Hello {displayName},\n\nYour verification code is: {code}\nIt expires at {expiresAt:u}.\n\nIf you did not attempt to sign in, reset your password and review account activity.";

        return SendAsync(email, displayName, subject, htmlBody, textBody, cancellationToken);
    }

    private async Task SendAsync(string email, string displayName, string subject, string htmlBody, string textBody, CancellationToken cancellationToken)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
        message.To.Add(new MailboxAddress(displayName, email));
        message.Subject = subject;
        message.Body = new BodyBuilder
        {
            HtmlBody = htmlBody,
            TextBody = textBody
        }.ToMessageBody();

        using var client = new SmtpClient();
        var socketOptions = _settings.UseStartTls
            ? SecureSocketOptions.StartTls
            : SecureSocketOptions.Auto;

        await client.ConnectAsync(_settings.Host, _settings.Port, socketOptions, cancellationToken);

        if (!string.IsNullOrWhiteSpace(_settings.Username))
        {
            await client.AuthenticateAsync(_settings.Username, _settings.Password ?? string.Empty, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        _logger.LogInformation("Sent account email '{Subject}' to {Email}", subject, email);
    }

    private static string Escape(string value)
    {
        return System.Net.WebUtility.HtmlEncode(value);
    }
}