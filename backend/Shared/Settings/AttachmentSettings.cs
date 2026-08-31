namespace Shared.Settings;

/// <summary>Limits applied to project task attachment uploads.</summary>
public sealed class AttachmentSettings
{
    /// <summary>Storage adapter name: Local or S3.</summary>
    public string StorageProvider { get; set; } = "Local";

    /// <summary>Absolute local storage root; empty uses the application content root outside production.</summary>
    public string RootPath { get; set; } = string.Empty;

    /// <summary>S3 bucket used for private attachment objects.</summary>
    public string S3BucketName { get; set; } = string.Empty;

    /// <summary>AWS region used when no custom S3 service URL is configured.</summary>
    public string S3Region { get; set; } = string.Empty;

    /// <summary>Optional S3-compatible endpoint, such as a private MinIO service URL.</summary>
    public string S3ServiceUrl { get; set; } = string.Empty;

    /// <summary>Whether S3 requests use path-style bucket addressing.</summary>
    public bool S3ForcePathStyle { get; set; }

    /// <summary>Optional access key for S3-compatible deployments; prefer workload credentials on AWS.</summary>
    public string S3AccessKey { get; set; } = string.Empty;

    /// <summary>Optional secret key for S3-compatible deployments; supply through secure configuration only.</summary>
    public string S3SecretKey { get; set; } = string.Empty;

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