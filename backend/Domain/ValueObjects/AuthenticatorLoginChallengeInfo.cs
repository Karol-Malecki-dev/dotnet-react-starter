namespace Domain.ValueObjects;

/// <summary>Public details for a pending authenticator verification step.</summary>
public sealed record AuthenticatorLoginChallengeInfo(Guid ChallengeId, DateTime ExpiresAt);