namespace Domain.ValueObjects;

public sealed record EmailTwoFactorChallengeDelivery(
    Guid ChallengeId,
    Guid UserId,
    string Email,
    string DisplayName,
    string Code,
    DateTime ExpiresAt);