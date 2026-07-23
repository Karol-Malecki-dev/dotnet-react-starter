using Domain.Enums.Auth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Auth
{
    /// <summary>Data required to consume a password reset request and set a new password.</summary>
    public class ResetPasswordRequestDto
    {
        /// <summary>Email address associated with the reset request.</summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>Password reset mechanism represented by this request.</summary>
        public ResetType ResetType { get; set; }

        /// <summary>Raw single-use link token. It is hashed before comparison and is never persisted as plain text.</summary>
        public string? Token { get; set; } = string.Empty;

        /// <summary>Optional reset code for flows that support code-based verification.</summary>
        public string? Code { get; set; } = string.Empty;

        /// <summary>New plain-text password that is validated and hashed by the backend.</summary>
        public string NewPassword { get; set; } = string.Empty;

    }
}
