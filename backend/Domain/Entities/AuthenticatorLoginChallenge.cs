namespace Domain.Entities;

/// <summary>Short-lived server-side challenge that binds a TOTP verification attempt to a password-validated login.</summary>
public sealed class AuthenticatorLoginChallenge
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public User User { get; set; } = null!;
}