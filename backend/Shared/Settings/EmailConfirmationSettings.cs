namespace Shared.Settings;

public class EmailConfirmationSettings
{
    public string PublicOrigin { get; set; } = "http://localhost:3000";

    public string ConfirmationPath { get; set; } = "/confirm-email";

    public int TokenExpiresInHours { get; set; } = 24;
}