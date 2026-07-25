using System.Net.Mail;

namespace Shared.Helpers;

public class EmailAddress
{
    public string Email { get; }
    public string EmailDomain { get; }

    public EmailAddress(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email address is required.", nameof(email));
        }

        var normalizedEmail = email.Trim();
        var parsedAddress = new MailAddress(normalizedEmail);
        if (!string.Equals(parsedAddress.Address, normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException("Email address has an invalid format.");
        }

        Email = normalizedEmail;
        EmailDomain = parsedAddress.Host;
    }
}
