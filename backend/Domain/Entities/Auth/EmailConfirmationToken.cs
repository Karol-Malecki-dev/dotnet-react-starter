namespace Domain.Entities.Auth;

/// <summary>Persisted single-use email confirmation token metadata.</summary>
public class EmailConfirmationToken
{
    /// <summary>Unique token record identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Identifier of the account being confirmed.</summary>
    public Guid UserId { get; set; }

    /// <summary>Hash of the raw token sent to the user.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>UTC time when the token was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC time after which the token cannot be consumed.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>UTC time when the token was successfully consumed, if applicable.</summary>
    public DateTime? ConsumedAt { get; set; }

    /// <summary>UTC time when the token was invalidated before consumption, if applicable.</summary>
    public DateTime? RevokedAt { get; set; }
}