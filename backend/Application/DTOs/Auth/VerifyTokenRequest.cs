using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Auth
{
    /// <summary>
    /// Request DTO for validating an access token.
    /// </summary>
    public class VerifyTokenRequest
    {
        /// <summary>Raw JWT supplied by the caller for validation.</summary>
        [Required(ErrorMessage = "Token is required")]
        public string Token { get; set; } = string.Empty;
    }
}
