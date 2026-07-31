using Application.Interfaces;

namespace IntegrationTests;

public class TestAccountEmailSender : IAccountEmailSender
{
    private readonly List<ConfirmationEmailMessage> _confirmationMessages = [];
    private readonly List<PasswordResetEmailMessage> _passwordResetMessages = [];
    private readonly List<TwoFactorCodeMessage> _twoFactorMessages = [];

    public IReadOnlyList<ConfirmationEmailMessage> Messages => _confirmationMessages;

    public IReadOnlyList<TwoFactorCodeMessage> TwoFactorMessages => _twoFactorMessages;

    public string? LatestConfirmationLink => _confirmationMessages.LastOrDefault()?.ConfirmationLink;

    public IReadOnlyList<PasswordResetEmailMessage> PasswordResetMessages => _passwordResetMessages;

    public string? LatestPasswordResetLink => _passwordResetMessages.LastOrDefault()?.ResetLink;

    public string? LatestTwoFactorCode => _twoFactorMessages.LastOrDefault()?.Code;

    public Task SendEmailConfirmationAsync(string email, string displayName, string confirmationLink)
    {
        _confirmationMessages.Add(new ConfirmationEmailMessage(email, displayName, confirmationLink));
        return Task.CompletedTask;
    }

    public Task SendPasswordResetLinkAsync(string email, string displayName, string resetLink)
    {
        _passwordResetMessages.Add(new PasswordResetEmailMessage(email, displayName, resetLink));
        return Task.CompletedTask;
    }

    public Task SendTwoFactorCodeAsync(string email, string displayName, string code, DateTime expiresAt)
    {
        _twoFactorMessages.Add(new TwoFactorCodeMessage(email, displayName, code, expiresAt));
        return Task.CompletedTask;
    }

    public void Clear()
    {
        _confirmationMessages.Clear();
        _passwordResetMessages.Clear();
        _twoFactorMessages.Clear();
    }
}

public sealed record ConfirmationEmailMessage(string Email, string DisplayName, string ConfirmationLink);

public sealed record PasswordResetEmailMessage(string Email, string DisplayName, string ResetLink);

public sealed record TwoFactorCodeMessage(string Email, string DisplayName, string Code, DateTime ExpiresAt);