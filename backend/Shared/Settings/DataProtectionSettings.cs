namespace Shared.Settings;

/// <summary>Configuration for the ASP.NET Core Data Protection key ring.</summary>
public class DataProtectionSettings
{
    /// <summary>Directory used to persist Data Protection keys. It must be durable in production.</summary>
    public string KeyRingPath { get; set; } = string.Empty;

    /// <summary>Application discriminator shared by instances that decrypt the same payloads.</summary>
    public string ApplicationName { get; set; } = "DotnetReactStarter";
}