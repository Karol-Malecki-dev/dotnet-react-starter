namespace Domain.ValueObjects;

/// <summary>One-time authenticator setup data returned only to the authenticated account owner.</summary>
public sealed record AuthenticatorSetup(string SharedKey, string ProvisioningUri);