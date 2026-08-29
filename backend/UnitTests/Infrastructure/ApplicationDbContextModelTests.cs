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
    public async Task User_email_converter_persists_canonical_value()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"email-converter-{Guid.NewGuid():N}")
            .Options;

        await using (var context = new ApplicationDbContext(options))
        {
            context.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Email = EmailAddress.Create(" User@Example.com "),
                DisplayName = "Email Converter User",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        }

        await using var verificationContext = new ApplicationDbContext(options);
        var user = await verificationContext.Users.SingleAsync();

        Assert.Equal("user@example.com", user.Email.Value);
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
