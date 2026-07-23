using Domain.Enums.Auth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities.Auth
{
    /// <summary>
    /// Persisted password reset request metadata.
    /// Raw reset tokens and codes are not stored; only their hashes are persisted.
    /// </summary>
    public class PasswordResetRequest
    {
        /// <summary>Unique identifier of the reset request.</summary>
        public Guid Id { get; set; }

        /// <summary>Identifier of the user who requested the password reset.</summary>
        public Guid UserId { get; set; }

        /// <summary>Reset mechanism represented by the request.</summary>
        public ResetType ResetType { get; set; }

        /// <summary>Hash of the raw link token, when a link-based reset is used.</summary>
        public string TokenHash { get; set; } = string.Empty;

        /// <summary>Hash of the reset code, when a code-based reset is used.</summary>
        public string CodeHash { get; set; } = string.Empty;

        /// <summary>UTC time when the request was created.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>UTC time after which the request can no longer be consumed.</summary>
        public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(15);

        /// <summary>UTC time when the latest reset message or code was sent.</summary>
        public DateTime? LastSentAt { get; set; }

        /// <summary>UTC time when the request was successfully consumed.</summary>
        public DateTime? ConsumedAt { get; set; }

        /// <summary>UTC time when the request was invalidated before consumption.</summary>
        public DateTime? RevokedAt { get; set; }

        /// <summary>Number of failed attempts associated with this reset request.</summary>
        public int FailedAttempts { get; set; } = 0;
    }
}
