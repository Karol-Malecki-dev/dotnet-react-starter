namespace Shared.Dtos;

/// <summary>
/// Non-sensitive runtime configuration exposed to frontend clients.
/// </summary>
public sealed class AppRuntimeConfigurationDto
{
    /// <summary>
    /// Feature flags safe to expose to the UI.
    /// </summary>
    public AppFeatureFlagsDto Features { get; init; } = new();
}

/// <summary>
/// Feature toggles that can be consumed by the frontend during bootstrap.
/// </summary>
public sealed class AppFeatureFlagsDto
{
    /// <summary>
    /// Indicates whether account email messages are delivered through the configured mail provider.
    /// </summary>
    public bool EmailDeliveryEnabled { get; init; }

    /// <summary>
    /// Indicates whether email-based two-factor authentication is enabled.
    /// </summary>
    public bool EmailTwoFactorEnabled { get; init; }

    /// <summary>
    /// Indicates whether newly created users must use email-based two-factor authentication.
    /// </summary>
    public bool EmailTwoFactorEnabledForNewUsers { get; init; }
}