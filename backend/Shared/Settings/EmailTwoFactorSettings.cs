namespace Shared.Settings;

/// <summary>Configuration for email-based two-factor authentication challenges.</summary>
public class EmailTwoFactorSettings
{
    /// <summary>Enables email 2FA for users whose accounts require it.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Sets email 2FA as the default for newly registered users.</summary>
    public bool EnableForNewUsers { get; set; } = true;

    /// <summary>Lifetime of a generated 2FA code in minutes.</summary>
    public int CodeExpiresInMinutes { get; set; } = 10;

    /// <summary>Number of digits generated for a 2FA code.</summary>
    public int CodeLength { get; set; } = 6;

    /// <summary>Maximum number of failed verification attempts for one challenge.</summary>
    public int MaxFailedAttempts { get; set; } = 5;
}