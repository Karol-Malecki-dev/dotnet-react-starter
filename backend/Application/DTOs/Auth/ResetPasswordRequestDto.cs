using Domain.Enums.Auth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Auth
{
    public class ResetPasswordRequestDto
    {
        public string Email { get; set; } = string.Empty;
        public ResetType ResetType { get; set; }
        public string? Token { get; set; } = string.Empty;
        public string? Code { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;

    }
}
