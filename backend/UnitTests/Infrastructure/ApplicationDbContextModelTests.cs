using Domain.Entities;
using Domain.Entities.Auth;
using Domain.Entities.JWT;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace UnitTests.Infrastructure;

public sealed class ApplicationDbContextModelTests
{
    [Fact]
    public void Model_contains_all_configured_entities()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nameof(Model_contains_all_configured_entities))
            .Options;

        using var context = new ApplicationDbContext(options);

        var configuredTypes = new[]
        {
            typeof(User),
            typeof(AccountSecurityEvent),
            typeof(RefreshToken),
            typeof(EmailConfirmationToken),
            typeof(EmailTwoFactorChallenge),
            typeof(PasswordResetRequest),
            typeof(Project),
            typeof(ProjectTask),
            typeof(ProjectMember)
        };

        Assert.All(configuredTypes, type => Assert.NotNull(context.Model.FindEntityType(type)));
    }

    [Fact]
    public void Account_security_event_keeps_user_references_optional_and_indexed()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nameof(Account_security_event_keeps_user_references_optional_and_indexed))
            .Options;

        using var context = new ApplicationDbContext(options);
        var securityEvent = context.Model.FindEntityType(typeof(AccountSecurityEvent));

        Assert.NotNull(securityEvent);
        Assert.True(securityEvent.FindProperty(nameof(AccountSecurityEvent.ActorUserId))!.IsNullable);
        Assert.True(securityEvent.FindProperty(nameof(AccountSecurityEvent.SubjectUserId))!.IsNullable);
        Assert.Contains(securityEvent.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual([
                nameof(AccountSecurityEvent.SubjectUserId),
                nameof(AccountSecurityEvent.OccurredAt)]));
    }

    [Fact]
    public void User_email_is_required_and_unique()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nameof(User_email_is_required_and_unique))
            .Options;

        using var context = new ApplicationDbContext(options);
        var user = context.Model.FindEntityType(typeof(User));

        Assert.NotNull(user);
        Assert.Equal(256, user.FindProperty(nameof(User.Email))!.GetMaxLength());
        Assert.Contains(user.GetIndexes(), index =>
            index.IsUnique && index.Properties.Single().Name == nameof(User.Email));
    }

    [Fact]
    public void User_display_name_is_required_and_limited()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nameof(User_display_name_is_required_and_limited))
            .Options;

        using var context = new ApplicationDbContext(options);
        var user = context.Model.FindEntityType(typeof(User));

        Assert.NotNull(user);
        Assert.Equal(DisplayName.MaxLength, user.FindProperty(nameof(User.DisplayName))!.GetMaxLength());
        Assert.False(user.FindProperty(nameof(User.DisplayName))!.IsNullable);
    }

    [Fact]
    public async Task User_email_converter_persists_canonical_value()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"email-converter-{Guid.NewGuid():N}")
            .Options;

        await using (var context = new ApplicationDbContext(options))
        {
            context.Users.Add(User.Create(
                EmailAddress.Create(" User@Example.com "),
                DisplayName.Create("Email Converter User"),
                isActive: true,
                id: Guid.NewGuid(),
                createdAt: DateTime.UtcNow));

            await context.SaveChangesAsync();
        }

        await using var verificationContext = new ApplicationDbContext(options);
        var user = await verificationContext.Users.SingleAsync();

        Assert.Equal("user@example.com", user.Email.Value);
    }

    [Fact]
    public async Task User_display_name_converter_persists_canonical_value()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"display-name-converter-{Guid.NewGuid():N}")
            .Options;

        await using (var context = new ApplicationDbContext(options))
        {
            context.Users.Add(User.Create(
                EmailAddress.Create("display-name@example.com"),
                DisplayName.Create("  Display   Name "),
                isActive: true,
                id: Guid.NewGuid(),
                createdAt: DateTime.UtcNow));

            await context.SaveChangesAsync();
        }

        await using var verificationContext = new ApplicationDbContext(options);
        var user = await verificationContext.Users.SingleAsync();

        Assert.Equal("Display Name", user.DisplayName.Value);
    }

    [Fact]
    public void Project_task_relationships_have_expected_delete_behavior()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nameof(Project_task_relationships_have_expected_delete_behavior))
            .Options;

        using var context = new ApplicationDbContext(options);
        var projectTask = context.Model.FindEntityType(typeof(ProjectTask));

        Assert.NotNull(projectTask);
        Assert.Contains(projectTask.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Project) &&
            foreignKey.DeleteBehavior == DeleteBehavior.Cascade);
        Assert.Contains(projectTask.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(User) &&
            foreignKey.DeleteBehavior == DeleteBehavior.SetNull);
    }
}
