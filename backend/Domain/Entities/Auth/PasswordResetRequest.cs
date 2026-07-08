using Domain.Enums.Auth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities.Auth
{
    public class PasswordResetRequest
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public ResetType ResetType { get; set; }

        public string TokenHash { get; set; } = string.Empty;
        public string CodeHash { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(15);
        public DateTime? LastSentAt { get; set; }
        public DateTime? ConsumedAt { get; set; }
        public DateTime? RevokedAt { get; set; }

        public int FailedAttempts { get; set; } = 0;
    }
}
