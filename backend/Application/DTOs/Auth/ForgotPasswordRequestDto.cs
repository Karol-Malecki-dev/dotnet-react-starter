using Domain.Enums.Auth;

namespace Application.DTOs.Auth
{
    public class ForgotPasswordRequestDto
    {
        public string Email { get; set; } = string.Empty;
        public ResetType ResetType { get; set; }

    }
}
