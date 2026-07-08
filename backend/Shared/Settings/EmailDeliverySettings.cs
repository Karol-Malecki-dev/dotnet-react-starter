namespace Shared.Settings;

public class EmailDeliverySettings
{
    public bool Enabled { get; set; }

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "Dotnet React Starter";

    public string? Username { get; set; }

    public string? Password { get; set; }

    public bool UseStartTls { get; set; } = true;
}