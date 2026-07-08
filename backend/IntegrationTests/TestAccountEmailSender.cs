using Application.Interfaces;

namespace IntegrationTests;

public class TestAccountEmailSender : IAccountEmailSender
{
    private readonly List<ConfirmationEmailMessage> _confirmationMessages = [];
    private readonly List<TwoFactorCodeMessage> _twoFactorMessages = [];

    public IReadOnlyList<ConfirmationEmailMessage> Messages => _confirmationMessages;

    public IReadOnlyList<TwoFactorCodeMessage> TwoFactorMessages => _twoFactorMessages;

    public string? LatestConfirmationLink => _confirmationMessages.LastOrDefault()?.ConfirmationLink;

    public string? LatestTwoFactorCode => _twoFactorMessages.LastOrDefault()?.Code;

    public Task SendEmailConfirmationAsync(string email, string displayName, string confirmationLink)
    {
        _confirmationMessages.Add(new ConfirmationEmailMessage(email, displayName, confirmationLink));
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
        _twoFactorMessages.Clear();
    }
}

public sealed record ConfirmationEmailMessage(string Email, string DisplayName, string ConfirmationLink);

public sealed record TwoFactorCodeMessage(string Email, string DisplayName, string Code, DateTime ExpiresAt);