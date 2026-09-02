namespace Shared.Settings;

/// <summary>
/// Controls database initialization behavior for the API process.
/// </summary>
public sealed class DatabaseSettings
{
    /// <summary>
    /// Gets or sets whether the normal API process applies pending migrations during startup.
    /// This must remain disabled in production, where a dedicated migration job is used.
    /// </summary>
    public bool ApplyMigrationsOnStartup { get; set; } = true;
}
