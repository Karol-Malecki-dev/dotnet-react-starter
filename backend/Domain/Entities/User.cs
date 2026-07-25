using Domain.Enums;
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

        /// <summary>User's email address; used for login and communication (must be unique)</summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>BCrypt hashed password for secure authentication</summary>
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>User's display name shown in the application (e.g., "John Doe")</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Optional URL to user's profile avatar/profile picture</summary>
        public string? AvatarUrl { get; set; }

        /// <summary>User's role determining permissions (Admin, User)</summary>
        public UserRole Role { get; set; }

        /// <summary>Indicates whether the user account is active or deactivated</summary>
        public bool IsActive { get; set; }

        /// <summary>Indicates whether the user's email address has been verified</summary>
        public bool IsEmailConfirmed { get; set; }

        /// <summary>Indicates whether the account requires email-based two-factor verification during sign-in</summary>
        public bool IsTwoFactorEnabled { get; set; }
        public string? Address { get; set; }

        /// <summary>Timestamp when the user account was created in UTC</summary>
        public DateTime CreatedAt { get; set; }

        public ICollection<ProjectMember> ProjectMemberships { get; set; } = [];

    }
}
