using Domain.Enums.Auth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Models.Results
{
    public class StartPasswordResetResult
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public string DestinationHint { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public ResetType ResetType { get; set; }
    }
}
