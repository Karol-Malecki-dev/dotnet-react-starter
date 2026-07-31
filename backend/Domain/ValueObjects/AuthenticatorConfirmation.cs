namespace Domain.ValueObjects;

/// <summary>Recovery codes returned once when an authenticator setup is confirmed.</summary>
public sealed record AuthenticatorConfirmation(IReadOnlyList<string> RecoveryCodes);