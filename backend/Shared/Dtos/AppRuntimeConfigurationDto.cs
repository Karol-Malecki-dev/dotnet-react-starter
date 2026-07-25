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
    public bool ProjectsEnabled { get; init; }

    public bool ProjectArchiveEnabled { get; init; }

    public bool ProjectTaskAssignmentEnabled { get; init; }

    /// <summary>
    /// Indicates whether account email messages are delivered through the configured mail provider.
    /// </summary>
    public bool EmailDeliveryEnabled { get; init; }

    /// <summary>
    /// Indicates whether the global quick search command bar should be visible in the frontend shell.
    /// </summary>
    public bool GlobalSearchEnabled { get; init; }

    /// <summary>
    /// Indicates whether the dashboard summary area should be visible to authenticated users.
    /// </summary>
    public bool DashboardOverviewEnabled { get; init; }

    /// <summary>
    /// Indicates whether admin-oriented navigation links should be exposed in the shell.
    /// </summary>
    public bool AdminNavigationEnabled { get; init; }

    /// <summary>
    /// Indicates whether the users management navigation entry should be exposed.
    /// </summary>
    public bool UserManagementNavigationEnabled { get; init; }

    /// <summary>
    /// Indicates whether email-related feature sections should be visible in the frontend.
    /// </summary>
    public bool EmailFeatureSectionsEnabled { get; init; }

    /// <summary>
    /// Indicates whether email-based two-factor authentication is enabled.
    /// </summary>
    public bool EmailTwoFactorEnabled { get; init; }

    /// <summary>
    /// Indicates whether newly created users must use email-based two-factor authentication.
    /// </summary>
    public bool EmailTwoFactorEnabledForNewUsers { get; init; }
}