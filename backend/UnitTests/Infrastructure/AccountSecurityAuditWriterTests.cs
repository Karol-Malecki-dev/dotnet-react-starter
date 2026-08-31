using System.Text.Json;
using Application.Interfaces;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace UnitTests.Infrastructure;

public sealed class AccountSecurityAuditWriterTests
{
    [Fact]
    public async Task Writer_persists_only_allowlisted_metadata()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nameof(Writer_persists_only_allowlisted_metadata))
            .Options;
        await using var context = new ApplicationDbContext(options);
        var writer = new AccountSecurityAuditWriter(context);

        await writer.WriteAsync(new AccountSecurityAuditEntry(
            "auth.login.failed",
            "failure",
            Metadata: new Dictionary<string, string>
            {
                ["reason"] = "invalid-credentials",
                ["password"] = "must-not-be-stored",
                ["token"] = "must-not-be-stored"
            }));

        var securityEvent = await context.AccountSecurityEvents.SingleAsync();
        Assert.NotNull(securityEvent.MetadataJson);
        Assert.Equal("invalid-credentials", JsonDocument.Parse(securityEvent.MetadataJson!).RootElement.GetProperty("reason").GetString());
        Assert.DoesNotContain("password", securityEvent.MetadataJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", securityEvent.MetadataJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Writer_supports_anonymous_events_without_user_ids()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nameof(Writer_supports_anonymous_events_without_user_ids))
            .Options;
        await using var context = new ApplicationDbContext(options);
        var writer = new AccountSecurityAuditWriter(context);

        await writer.WriteAsync(new AccountSecurityAuditEntry("auth.login.failed", "failure"));

        var securityEvent = await context.AccountSecurityEvents.SingleAsync();
        Assert.Null(securityEvent.ActorUserId);
        Assert.Null(securityEvent.SubjectUserId);
    }

    [Fact]
    public async Task Writer_rejects_metadata_that_exceeds_the_persistence_limit()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nameof(Writer_rejects_metadata_that_exceeds_the_persistence_limit))
            .Options;
        await using var context = new ApplicationDbContext(options);
        var writer = new AccountSecurityAuditWriter(context);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => writer.WriteAsync(new AccountSecurityAuditEntry(
            "auth.login.failed",
            "failure",
            Metadata: new Dictionary<string, string>
            {
                ["reason"] = new string('x', 4_000)
            })));

        Assert.Contains("Metadata exceeds", exception.Message);
        Assert.Empty(context.AccountSecurityEvents);
    }
}