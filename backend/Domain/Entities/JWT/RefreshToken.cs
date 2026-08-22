using Domain.Enums;
using System;

namespace Domain.Entities.JWT
{
    /// <summary>
    /// Persisted refresh token with user snapshot for rotation.
    /// Long live span (e.g. 7 days) and stored securely in database.
    /// </summary>
    public class RefreshToken
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public string UserEmail { get; set; } = string.Empty;

        public string UserDisplayName { get; set; } = string.Empty;

        public UserRole UserRole { get; set; }

        public bool IsEmailConfirmed { get; set; }

        public string TokenHash { get; set; } = string.Empty;

        public string ConcurrencyStamp { get; set; } = string.Empty;

        /// <summary>
        /// IP address from which the token was created (for audit trail).
        /// </summary>
        public string CreatedByIp { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// When the token was last used (null if never used).
        /// </summary>
        public DateTime? LastUsedAt { get; set; }

        /// <summary>
        /// IP address from which the token was last used (null if never used).
        /// Helps detect anomalies if token is used from different IP.
        /// </summary>
        public string? LastUsedByIp { get; set; }

        public DateTime? RevokedAt { get; set; }

        /// <summary>
        /// Reason for token revocation (null if token is still active).
        /// </summary>
        public RevocationReason? RevocationReason { get; set; }

        public string? ReplacedByTokenHash { get; set; }

        /// <summary>
        /// Family ID for token rotation chain. Groups related tokens together.
        /// Null for standalone tokens or legacy tokens without family tracking.
        /// </summary>
        public Guid? FamilyId { get; set; }
    }
}
