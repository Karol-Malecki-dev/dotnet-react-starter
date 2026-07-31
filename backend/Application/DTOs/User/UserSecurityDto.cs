using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.User
{
    public class UserSecurityDto
    {
        public string Email{ get; set; } = string.Empty;
        public bool IsEmailConfirmed{ get; set; } 
        public bool IsTwoFactorEnabled { get; set; }
        public bool IsAuthenticatorEnabled { get; set; }
    }
}
