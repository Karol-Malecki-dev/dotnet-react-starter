using API.Middleware;
using API.Services;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Serilog;
using Shared.Settings;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog structured logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/app-.txt",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}",
        retainedFileCountLimit: 7
    )
    .CreateLogger();

try
{
    Log.Information("🚀 Application starting up...");

    builder.Host.UseSerilog();
    builder.Services.AddProjectServices(builder.Configuration, builder.Environment);

    var app = builder.Build();
    var databaseSettings = app.Services.GetRequiredService<IOptions<DatabaseSettings>>().Value;
    var migrateOnly = args.Any(argument =>
        string.Equals(argument, "--migrate-only", StringComparison.OrdinalIgnoreCase));

    Log.Information("📊 Configuring application middleware...");

    app.UseForwardedHeaders();

    if (migrateOnly || databaseSettings.ApplyMigrationsOnStartup)
    {
        await ApplyDatabaseMigrationsAsync(
            app.Services,
            app.Lifetime.ApplicationStopping);
    }

    if (migrateOnly)
    {
        Log.Information("Database migration job completed successfully.");
        return;
    }

    // Configure the HTTP request pipeline
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
        Log.Information("📖 Swagger UI available at /swagger");
    }

    app.UseHttpsRedirection();

    // JWT Authentication & Authorization
    app.UseCors("ReactApp");

    app.UseRateLimiter();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        Predicate = healthCheck => !healthCheck.Tags.Contains("workers")
    });
    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = healthCheck => healthCheck.Tags.Contains("ready")
    });
    app.MapHealthChecks("/health/workers", new HealthCheckOptions
    {
        Predicate = healthCheck => healthCheck.Tags.Contains("workers")
    });
    app.MapHealthChecks("/health/storage", new HealthCheckOptions
    {
        Predicate = healthCheck => healthCheck.Tags.Contains("object-storage")
    });
    app.MapHealthChecks("/health/malware-scanner", new HealthCheckOptions
    {
        Predicate = healthCheck => healthCheck.Tags.Contains("malware-scanner")
    });
    app.MapHealthChecks("/health/email", new HealthCheckOptions
    {
        Predicate = healthCheck => healthCheck.Tags.Contains("email")
    });
    app.MapControllers();

    Log.Information("🌐 Application listening on configured ports");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "💥 Application terminated unexpectedly");
    Environment.ExitCode = 1;
}
finally
{
    Log.Information("🛑 Application shutting down...");
    await Log.CloseAndFlushAsync();
}

static async Task ApplyDatabaseMigrationsAsync(
    IServiceProvider services,
    CancellationToken cancellationToken)
{
    await using var scope = services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    try
    {
        if (dbContext.Database.IsRelational())
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
            Log.Information("Database migrations applied successfully.");
            return;
        }

        Log.Information("Database initialization skipped for a non-relational provider.");
    }
    catch (Exception exception)
    {
        Log.Error(exception, "Database migration failed.");
        throw;
    }
}
