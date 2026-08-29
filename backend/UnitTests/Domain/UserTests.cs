using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

namespace UnitTests.Domain;

public sealed class UserTests
{
    [Fact]
    public void Create_sets_initial_profile_and_account_state()
    {
        var id = Guid.NewGuid();
        var createdAt = new DateTime(2026, 8, 30, 12, 0, 0, DateTimeKind.Utc);

        var user = User.Create(
            EmailAddress.Create(" Admin@Example.com "),
            DisplayName.Create("  Admin User  "),
            UserRole.Admin,
            isActive: false,
            isEmailConfirmed: true,
            isTwoFactorEnabled: true,
            id: id,
            createdAt: createdAt);

        Assert.Equal(id, user.Id);
        Assert.Equal("admin@example.com", user.Email.Value);
        Assert.Equal("Admin User", user.DisplayName.Value);
        Assert.Equal(UserRole.Admin, user.Role);
        Assert.False(user.IsActive);
        Assert.True(user.IsEmailConfirmed);
        Assert.True(user.IsTwoFactorEnabled);
        Assert.Equal(createdAt, user.CreatedAt);
        Assert.False(string.IsNullOrWhiteSpace(user.ConcurrencyStamp));
    }

    [Fact]
    public void Create_rejects_empty_identifier_and_undefined_role()
    {
        Assert.Throws<ArgumentException>(() => User.Create(
            EmailAddress.Create("user@example.com"),
            DisplayName.Create("User"),
            id: Guid.Empty));

        Assert.Throws<ArgumentOutOfRangeException>(() => User.Create(
            EmailAddress.Create("user@example.com"),
            DisplayName.Create("User"),
            (UserRole)999));
    }

    [Fact]
    public void Profile_methods_update_profile_state()
    {
        var user = CreateUser();
        var email = EmailAddress.Create("updated@example.com");
        var displayName = DisplayName.Create("Updated User");

        user.ChangeEmail(email);
        user.ChangeDisplayName(displayName);
        user.ChangeAvatarUrl("https://example.com/avatar.png");
        user.ChangeAddress("Updated address");

        Assert.Equal(email, user.Email);
        Assert.Equal(displayName, user.DisplayName);
        Assert.Equal("https://example.com/avatar.png", user.AvatarUrl);
        Assert.Equal("Updated address", user.Address);
    }

    [Fact]
    public void Password_change_clears_login_failures_and_rotates_stamp()
    {
        var user = CreateUser();
        user.RecordFailedLogin(DateTime.UtcNow, 1, TimeSpan.FromMinutes(5));
        var initialStamp = user.ConcurrencyStamp;

        user.ChangePasswordHash("new-password-hash");

        Assert.Equal("new-password-hash", user.PasswordHash);
        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Null(user.LockoutEndAt);
        Assert.NotEqual(initialStamp, user.ConcurrencyStamp);
    }

    [Fact]
    public void DisableAuthenticator_clears_secret_and_enabled_state()
    {
        var user = CreateUser();

        user.SetProtectedAuthenticatorSecret("protected-secret");
        user.EnableAuthenticator();
        user.DisableAuthenticator();

        Assert.False(user.IsAuthenticatorEnabled);
        Assert.Null(user.ProtectedAuthenticatorSecret);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SetPasswordHash_rejects_blank_hash(string passwordHash)
    {
        var user = CreateUser();

        Assert.Throws<ArgumentException>(() => user.SetPasswordHash(passwordHash));
    }

    private static User CreateUser()
        => User.Create(
            EmailAddress.Create("user@example.com"),
            DisplayName.Create("User"));
}
