using Domain.Enums;
using Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
    public class User
    {
        /// <summary>Unique identifier for the user (primary key)</summary>
        public Guid Id { get; set; }

        /// <summary>User's normalized email address; used for login and communication (must be unique)</summary>
        public EmailAddress Email { get; set; } = null!;

        /// <summary>BCrypt hashed password for secure authentication</summary>
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>User's normalized display name shown in the application (e.g., "John Doe")</summary>
        public DisplayName DisplayName { get; set; } = null!;

        /// <summary>Optional URL to user's profile avatar/profile picture</summary>
        public string? AvatarUrl { get; set; }

        /// <summary>User's role determining permissions (Admin, User)</summary>
        public UserRole Role { get; set; }

        /// <summary>Indicates whether the user account is active or deactivated</summary>
        public bool IsActive { get; set; }

        /// <summary>Application-managed value used to detect concurrent authentication-state updates.</summary>
        public string ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>Number of consecutive invalid password-login attempts for the account.</summary>
        public int FailedLoginAttempts { get; set; }

        /// <summary>UTC timestamp until which password login is blocked, when a lockout is active.</summary>
        public DateTime? LockoutEndAt { get; set; }

        /// <summary>Indicates whether the user's email address has been verified</summary>
        public bool IsEmailConfirmed { get; set; }

        /// <summary>Indicates whether the account requires email-based two-factor verification during sign-in</summary>
        public bool IsTwoFactorEnabled { get; set; }

        /// <summary>Indicates whether an authenticator application has been confirmed for the account.</summary>
        public bool IsAuthenticatorEnabled { get; set; }

        /// <summary>Data Protection-encrypted TOTP secret. The raw secret is never stored in the database.</summary>
        public string? ProtectedAuthenticatorSecret { get; set; }
        public string? Address { get; set; }

        /// <summary>Timestamp when the user account was created in UTC</summary>
        public DateTime CreatedAt { get; set; }

        public ICollection<ProjectMember> ProjectMemberships { get; set; } = [];

        public ICollection<AuthenticatorRecoveryCode> AuthenticatorRecoveryCodes { get; set; } = [];

    }
}
