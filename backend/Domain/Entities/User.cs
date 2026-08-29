using Domain.Enums;
using Domain.ValueObjects;
using System;
using System.Collections.Generic;

namespace Domain.Entities;

/// <summary>
/// Aggregate root for a user's profile and account security state.
/// </summary>
public class User
{
    private User()
    {
    }

    /// <summary>
    /// Creates a user with validated profile identity and initial account state.
    /// </summary>
    public static User Create(
        EmailAddress email,
        DisplayName displayName,
        UserRole role = UserRole.User,
        bool isActive = true,
        bool isEmailConfirmed = false,
        bool isTwoFactorEnabled = false,
        Guid? id = null,
        DateTime? createdAt = null)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(displayName);

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), role, "The user role is not defined.");
        }

        if (id == Guid.Empty)
        {
            throw new ArgumentException("The user identifier cannot be empty.", nameof(id));
        }

        return new User
        {
            Id = id ?? Guid.NewGuid(),
            Email = email,
            DisplayName = displayName,
            Role = role,
            IsActive = isActive,
            IsEmailConfirmed = isEmailConfirmed,
            IsTwoFactorEnabled = isTwoFactorEnabled,
            CreatedAt = createdAt ?? DateTime.UtcNow
        };
    }

    /// <summary>Unique identifier for the user (primary key).</summary>
    public Guid Id { get; private set; }

    /// <summary>User's normalized email address used for login and communication.</summary>
    public EmailAddress Email { get; private set; } = null!;

    /// <summary>BCrypt or Identity password hash used for authentication.</summary>
    public string PasswordHash { get; private set; } = string.Empty;

    /// <summary>User's normalized display name shown in the application.</summary>
    public DisplayName DisplayName { get; private set; } = null!;

    /// <summary>Optional URL to the user's profile avatar.</summary>
    public string? AvatarUrl { get; private set; }

    /// <summary>User's role determining permissions.</summary>
    public UserRole Role { get; private set; }

    /// <summary>Indicates whether the user account is active or deactivated.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Application-managed value used to detect concurrent authentication-state updates.</summary>
    public string ConcurrencyStamp { get; private set; } = Guid.NewGuid().ToString("N");

    /// <summary>Number of consecutive invalid password-login attempts for the account.</summary>
    public int FailedLoginAttempts { get; private set; }

    /// <summary>UTC timestamp until which password login is blocked, when a lockout is active.</summary>
    public DateTime? LockoutEndAt { get; private set; }

    /// <summary>Indicates whether the user's email address has been verified.</summary>
    public bool IsEmailConfirmed { get; private set; }

    /// <summary>Indicates whether the account requires email-based two-factor verification during sign-in.</summary>
    public bool IsTwoFactorEnabled { get; private set; }

    /// <summary>Indicates whether an authenticator application has been confirmed for the account.</summary>
    public bool IsAuthenticatorEnabled { get; private set; }

    /// <summary>Data Protection-encrypted TOTP secret. The raw secret is never stored in the database.</summary>
    public string? ProtectedAuthenticatorSecret { get; private set; }

    /// <summary>Optional profile address.</summary>
    public string? Address { get; private set; }

    /// <summary>Timestamp when the user account was created in UTC.</summary>
    public DateTime CreatedAt { get; private set; }

    public ICollection<ProjectMember> ProjectMemberships { get; private set; } = [];

    public ICollection<AuthenticatorRecoveryCode> AuthenticatorRecoveryCodes { get; private set; } = [];

    /// <summary>Changes the profile email after application-level uniqueness validation.</summary>
    public void ChangeEmail(EmailAddress email)
    {
        ArgumentNullException.ThrowIfNull(email);
        Email = email;
    }

    /// <summary>Changes the normalized profile display name.</summary>
    public void ChangeDisplayName(DisplayName displayName)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        DisplayName = displayName;
    }

    /// <summary>Changes or clears the profile avatar URL.</summary>
    public void ChangeAvatarUrl(string? avatarUrl)
    {
        AvatarUrl = avatarUrl;
    }

    /// <summary>Changes or clears the profile address.</summary>
    public void ChangeAddress(string? address)
    {
        Address = address;
    }

    /// <summary>Sets a password hash during account creation or controlled data setup.</summary>
    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("The password hash is required.", nameof(passwordHash));
        }

        PasswordHash = passwordHash;
    }

    /// <summary>Changes a password hash and clears password-login lockout state.</summary>
    public void ChangePasswordHash(string passwordHash)
    {
        SetPasswordHash(passwordHash);
        ResetLoginFailures();
        RefreshAuthenticationConcurrencyStamp();
    }

    /// <summary>Changes the authorization role.</summary>
    public void ChangeRole(UserRole role)
    {
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), role, "The user role is not defined.");
        }

        Role = role;
    }

    /// <summary>Activates the account.</summary>
    public void Activate()
    {
        IsActive = true;
    }

    /// <summary>Deactivates the account.</summary>
    public void Deactivate()
    {
        IsActive = false;
    }

    /// <summary>Sets the email confirmation state.</summary>
    public void SetEmailConfirmed(bool isConfirmed)
    {
        IsEmailConfirmed = isConfirmed;
    }

    /// <summary>Sets the email two-factor preference.</summary>
    public void SetTwoFactorEnabled(bool enabled)
    {
        IsTwoFactorEnabled = enabled;
    }

    /// <summary>Stores the protected authenticator secret generated during setup.</summary>
    public void SetProtectedAuthenticatorSecret(string? protectedSecret)
    {
        ProtectedAuthenticatorSecret = protectedSecret;
    }

    /// <summary>Marks the authenticator application as confirmed.</summary>
    public void EnableAuthenticator()
    {
        IsAuthenticatorEnabled = true;
    }

    /// <summary>Disables the authenticator application and removes its protected secret.</summary>
    public void DisableAuthenticator()
    {
        IsAuthenticatorEnabled = false;
        ProtectedAuthenticatorSecret = null;
    }

    /// <summary>Clears consecutive password-login failures and an expired lockout.</summary>
    public void ResetLoginFailures()
    {
        var stateChanged = FailedLoginAttempts != 0 || LockoutEndAt.HasValue;
        FailedLoginAttempts = 0;
        LockoutEndAt = null;

        if (stateChanged)
        {
            RefreshAuthenticationConcurrencyStamp();
        }
    }

    /// <summary>Records a failed password login and applies the configured lockout policy.</summary>
    public void RecordFailedLogin(DateTime now, int maxFailedLoginAttempts, TimeSpan lockoutDuration)
    {
        if (maxFailedLoginAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxFailedLoginAttempts), maxFailedLoginAttempts, "The maximum must be at least one.");
        }

        if (lockoutDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lockoutDuration), lockoutDuration, "The lockout duration cannot be negative.");
        }

        FailedLoginAttempts = Math.Min(FailedLoginAttempts + 1, maxFailedLoginAttempts);
        if (FailedLoginAttempts >= maxFailedLoginAttempts)
        {
            LockoutEndAt = now.Add(lockoutDuration);
        }

        RefreshAuthenticationConcurrencyStamp();
    }

    /// <summary>Rotates the authentication-state concurrency stamp.</summary>
    public void RefreshAuthenticationConcurrencyStamp()
    {
        ConcurrencyStamp = Guid.NewGuid().ToString("N");
    }
}
