using Domain.Entities;
using Domain.Entities.JWT;
using Domain.Enums;
using Domain.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace IntegrationTests;

[Collection(nameof(PostgreSqlIntegrationTestCollection))]
public sealed class PostgreSqlIntegrationTests
{
    private readonly PostgreSqlWebApplicationFactory _factory;

    public PostgreSqlIntegrationTests(PostgreSqlWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostgreSql_container_applies_all_migrations_and_serves_health_check()
    {
        using var client = _factory.CreateClient();

        var healthResponse = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", dbContext.Database.ProviderName);

        var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();
        var knownMigrations = dbContext.Database.GetMigrations();
        Assert.Equal(knownMigrations.Order(), appliedMigrations.Order());
    }

    [Fact]
    public async Task PostgreSql_refresh_rotation_accepts_only_one_concurrent_successor()
    {
        await SeedUserAsync();

        JwtTokens initialTokens;
        await using (var setupScope = _factory.Services.CreateAsyncScope())
        {
            var serviceProvider = setupScope.ServiceProvider;
            var tokenService = serviceProvider.GetRequiredService<IJwtTokenService>();
            var dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await dbContext.Users
                .SingleAsync(candidate => candidate.Email == "postgres-concurrent@example.com");

            initialTokens = await tokenService.GenerateTokensAsync(user);
        }

        await using var firstScope = _factory.Services.CreateAsyncScope();
        await using var secondScope = _factory.Services.CreateAsyncScope();
        var firstTokenService = firstScope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var secondTokenService = secondScope.ServiceProvider.GetRequiredService<IJwtTokenService>();

        var refreshResults = await Task.WhenAll(
            firstTokenService.RefreshTokensAsync(initialTokens.RefreshToken),
            secondTokenService.RefreshTokensAsync(initialTokens.RefreshToken));

        Assert.Single(refreshResults, tokens => tokens is not null);

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var storedTokens = await verificationContext.RefreshTokens.ToListAsync();

        Assert.Equal(2, storedTokens.Count);
        Assert.Single(storedTokens, token => token.RevocationReason == RevocationReason.TokenRotated);
        Assert.Single(storedTokens, token => token.RevocationReason == RevocationReason.RefreshTokenReplay);
        Assert.DoesNotContain(storedTokens, token => !token.RevokedAt.HasValue);
    }

    private async Task SeedUserAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "postgres-concurrent@example.com",
            DisplayName = "PostgreSQL Concurrent User",
            Role = UserRole.User,
            IsActive = true,
            IsEmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
    }
}