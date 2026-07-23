namespace Shared.Settings;

/// <summary>Configuration for links and expiration of email confirmation tokens.</summary>
public class EmailConfirmationSettings
{
    /// <summary>Public frontend origin used when building confirmation links.</summary>
    public string PublicOrigin { get; set; } = "http://localhost:3000";

    /// <summary>Frontend route that consumes the confirmation link.</summary>
    public string ConfirmationPath { get; set; } = "/confirm-email";

    /// <summary>Lifetime of an email confirmation token in hours.</summary>
    public int TokenExpiresInHours { get; set; } = 24;
}