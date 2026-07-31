namespace Domain.Entities;

/// <summary>Single-use recovery code for an authenticator application.</summary>
public sealed class AuthenticatorRecoveryCode
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public User User { get; set; } = null!;
}