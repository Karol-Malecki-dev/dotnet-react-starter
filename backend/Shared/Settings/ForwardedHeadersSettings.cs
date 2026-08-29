namespace Shared.Settings;

/// <summary>Configuration for accepting client information from a trusted reverse proxy.</summary>
public class ForwardedHeadersSettings
{
    /// <summary>Enables processing of X-Forwarded-For and X-Forwarded-Proto headers.</summary>
    public bool Enabled { get; set; }

    /// <summary>Trusted proxy IP addresses. Values must be IP address literals.</summary>
    public string[] KnownProxies { get; set; } = Array.Empty<string>();

    /// <summary>Trusted proxy networks in CIDR notation, for example 172.28.0.0/16.</summary>
    public string[] KnownNetworks { get; set; } = Array.Empty<string>();

    /// <summary>Maximum number of forwarded values accepted from the proxy chain.</summary>
    public int ForwardLimit { get; set; } = 1;
}