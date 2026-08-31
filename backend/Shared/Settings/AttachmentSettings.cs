namespace Shared.Settings;

/// <summary>Limits applied to project task attachment uploads.</summary>
public sealed class AttachmentSettings
{
    /// <summary>Absolute local storage root; empty uses the application content root outside production.</summary>
    public string RootPath { get; set; } = string.Empty;

    /// <summary>Maximum size of one attachment in bytes.</summary>
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>Maximum number of attachments allowed for one task.</summary>
    public int MaxCountPerTask { get; set; } = 20;

    /// <summary>Maximum combined attachment size allowed for one task in bytes.</summary>
    public long MaxBytesPerTask { get; set; } = 100 * 1024 * 1024;

    /// <summary>Whether uploads must receive a clean result from the configured malware scanner.</summary>
    public bool RequireMalwareScan { get; set; }

    /// <summary>ClamAV daemon host. Empty keeps the fail-closed unavailable adapter.</summary>
    public string MalwareScannerHost { get; set; } = string.Empty;

    /// <summary>ClamAV daemon TCP port.</summary>
    public int MalwareScannerPort { get; set; } = 3310;

    /// <summary>Maximum time allowed for one ClamAV scan.</summary>
    public int MalwareScannerTimeoutSeconds { get; set; } = 30;
}