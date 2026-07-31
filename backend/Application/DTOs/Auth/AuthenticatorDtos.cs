using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Auth;

/// <summary>Contains the one-time shared key and provisioning URI for an authenticator application.</summary>
public sealed class AuthenticatorSetupDto
{
    public string SharedKey { get; set; } = string.Empty;
    public string ProvisioningUri { get; set; } = string.Empty;
}

/// <summary>Confirms ownership of the authenticator application by validating its current TOTP code.</summary>
public sealed class ConfirmAuthenticatorSetupRequestDto
{
    [Required]
    [StringLength(10, MinimumLength = 6)]
    public string Code { get; set; } = string.Empty;
}

/// <summary>Recovery codes returned exactly once after a successful authenticator confirmation.</summary>
public sealed class AuthenticatorConfirmationDto
{
    public IReadOnlyList<string> RecoveryCodes { get; set; } = [];
}

/// <summary>Disables the authenticator application after re-authenticating with password and a current or recovery code.</summary>
public sealed class DisableAuthenticatorRequestDto
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [StringLength(64, MinimumLength = 6)]
    public string Code { get; set; } = string.Empty;
}

/// <summary>Regenerates recovery codes after re-authenticating with password and a current or recovery code.</summary>
public sealed class RegenerateAuthenticatorRecoveryCodesRequestDto
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [StringLength(64, MinimumLength = 6)]
    public string Code { get; set; } = string.Empty;
}