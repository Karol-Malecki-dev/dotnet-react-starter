using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Admin
{
    public class AdminUserFilterRequestDto
    {
        public List<Guid>? Ids { get; set; }
        public List<string>? Emails { get; set; }
        public List<UserRole>? Roles { get; set; }

        public bool? IsActive { get; set; }
        public bool? IsEmailConfirmed { get; set; }
        public bool? IsTwoFactorEnabled { get; set; }

        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
