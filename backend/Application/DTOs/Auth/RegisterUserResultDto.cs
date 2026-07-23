namespace Application.DTOs.Auth;

/// <summary>Result returned after a registration request has been processed.</summary>
public class RegisterUserResultDto
{
    /// <summary>Email address associated with the newly created account.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Indicates whether the user must confirm the email address before logging in.</summary>
    public bool RequiresEmailConfirmation { get; set; }
}