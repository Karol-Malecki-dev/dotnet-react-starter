namespace Shared.Settings;

public class EmailTwoFactorSettings
{
    public bool Enabled { get; set; } = true;

    public bool EnableForNewUsers { get; set; } = true;

    public int CodeExpiresInMinutes { get; set; } = 10;

    public int CodeLength { get; set; } = 6;

    public int MaxFailedAttempts { get; set; } = 5;
}