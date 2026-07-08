namespace Application.Interfaces;

public interface IAccountEmailSender
{
    Task SendEmailConfirmationAsync(string email, string displayName, string confirmationLink);

    Task SendTwoFactorCodeAsync(string email, string displayName, string code, DateTime expiresAt);
}