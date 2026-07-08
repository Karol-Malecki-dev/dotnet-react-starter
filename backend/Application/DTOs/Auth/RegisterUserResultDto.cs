namespace Application.DTOs.Auth;

public class RegisterUserResultDto
{
    public string Email { get; set; } = string.Empty;

    public bool RequiresEmailConfirmation { get; set; }
}