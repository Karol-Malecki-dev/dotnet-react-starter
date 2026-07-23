namespace Domain.Entities.Auth;

/// <summary>Persisted state for a short-lived email-based two-factor challenge.</summary>
public class EmailTwoFactorChallenge
{
    /// <summary>Unique challenge identifier sent back to the client.</summary>
    public Guid Id { get; set; }

    /// <summary>Identifier of the user completing the sign-in flow.</summary>
    public Guid UserId { get; set; }

    /// <summary>Hash of the code sent by email.</summary>
    public string CodeHash { get; set; } = string.Empty;

    /// <summary>UTC time when the challenge was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC time when the current code was most recently sent.</summary>
    public DateTime LastSentAt { get; set; }

    /// <summary>UTC time after which the code cannot be accepted.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>UTC time when the challenge was successfully consumed, if applicable.</summary>
    public DateTime? ConsumedAt { get; set; }

    /// <summary>UTC time when the challenge was invalidated before consumption, if applicable.</summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>Number of failed code verification attempts.</summary>
   
    public int FailedAttempts { get; set; }
}