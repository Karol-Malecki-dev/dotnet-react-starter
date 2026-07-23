namespace Shared.Settings;

/// <summary>Configuration for the SMTP email delivery implementation.</summary>
public class EmailDeliverySettings
{
    /// <summary>When false, the logging sender is used instead of external SMTP delivery.</summary>
    public bool Enabled { get; set; }

    /// <summary>SMTP host name.</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>SMTP port.</summary>
    public int Port { get; set; } = 587;

    /// <summary>Envelope sender email address.</summary>
    public string FromAddress { get; set; } = string.Empty;

    /// <summary>Display name used for outgoing account emails.</summary>
    public string FromName { get; set; } = "Dotnet React Starter";

    /// <summary>Optional SMTP username.</summary>
    public string? Username { get; set; }

    /// <summary>Optional SMTP password. Provide it through secrets or environment configuration.</summary>
    public string? Password { get; set; }

    /// <summary>Whether the SMTP client should use STARTTLS.</summary>
    public bool UseStartTls { get; set; } = true;
}