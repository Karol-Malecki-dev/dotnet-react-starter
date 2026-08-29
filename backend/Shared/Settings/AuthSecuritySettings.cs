namespace Shared.Settings;

/// <summary>Configuration for public authentication abuse protection and password-login lockout.</summary>
public class AuthSecuritySettings
{
    /// <summary>Maximum number of requests allowed for one client IP and auth endpoint during the window.</summary>
    public int RateLimitPermitLimit { get; set; } = 5;

    /// <summary>Length of the fixed rate-limit window in seconds.</summary>
    public int RateLimitWindowSeconds { get; set; } = 60;

    /// <summary>Number of consecutive invalid passwords before the account is temporarily locked.</summary>
    public int MaxFailedLoginAttempts { get; set; } = 5;

    /// <summary>Duration of a password-login lockout in minutes.</summary>
    public int LockoutDurationMinutes { get; set; } = 15;
}