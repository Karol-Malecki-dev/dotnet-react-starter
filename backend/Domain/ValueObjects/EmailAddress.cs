using System.Net.Mail;

namespace Domain.ValueObjects;

/// <summary>
/// Normalized and validated email address used by the domain model.
/// </summary>
public sealed record EmailAddress : IComparable<EmailAddress>
{
    /// <summary>
    /// Maximum length accepted by the account email column and common email providers.
    /// </summary>
    public const int MaxLength = 256;

    private EmailAddress(string value)
    {
        Value = value;
        Domain = new MailAddress(value).Host.ToLowerInvariant();
    }

    /// <summary>
    /// Gets the canonical lowercase email address.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets the lowercase domain portion of the email address.
    /// </summary>
    public string Domain { get; }

    /// <summary>
    /// Creates an email address after trimming and lowercasing the input.
    /// </summary>
    /// <param name="email">Raw email address supplied by a caller.</param>
    /// <returns>A normalized email address.</returns>
    /// <exception cref="ArgumentException">Thrown when the value is empty, too long, or malformed.</exception>
    public static EmailAddress Create(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email address is required.", nameof(email));
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (normalizedEmail.Length > MaxLength)
        {
            throw new ArgumentException($"Email address cannot exceed {MaxLength} characters.", nameof(email));
        }

        MailAddress parsedAddress;
        try
        {
            parsedAddress = new MailAddress(normalizedEmail);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("Email address has an invalid format.", nameof(email), exception);
        }

        if (!string.Equals(parsedAddress.Address, normalizedEmail, StringComparison.Ordinal))
        {
            throw new ArgumentException("Email address has an invalid format.", nameof(email));
        }

        return new EmailAddress(normalizedEmail);
    }

    /// <summary>
    /// Attempts to create an email address without throwing for invalid external input.
    /// </summary>
    public static bool TryCreate(string? email, out EmailAddress? address)
    {
        try
        {
            address = email is null ? null : Create(email);
            return address is not null;
        }
        catch (ArgumentException)
        {
            address = null;
            return false;
        }
    }

    public int CompareTo(EmailAddress? other)
        => other is null ? 1 : StringComparer.Ordinal.Compare(Value, other.Value);

    public override string ToString() => Value;
}
