using Domain.Enums.Auth;

namespace Application.DTOs.Auth
{
    /// <summary>
    /// Request that starts the password reset flow.
    /// The endpoint should return a neutral response whether or not the email exists.
    /// </summary>
    public class ForgotPasswordRequestDto
    {
        /// <summary>Email address for the account that may receive a reset link or code.</summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>Reset mechanism requested by the client, such as a link-based flow.</summary>
        public ResetType ResetType { get; set; }

    }
}
