namespace Application.DTOs.Auth;

public class TwoFactorChallengeResponseDto
{
    public bool RequiresTwoFactor { get; set; } = true;

    public Guid ChallengeId { get; set; }

    public string DestinationHint { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
}