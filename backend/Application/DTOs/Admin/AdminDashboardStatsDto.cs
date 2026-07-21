using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Admin
{
    public class AdminDashboardStatsDto
    {
        // General
        public int TotalUsers { get; set; }
        //User
        public int ActiveUsers { get; set; }
        public int InactiveUsers { get; set; }
        public int NewUsersLast7Days { get; set; }
        // Admin
        public int AdminUsers { get; set; }
        public int ActiveAdminUsers { get; set; }
    }
}
