using API.Filters;
using API.Middleware;
using API.Configurations;
using Application.Interfaces;
using Application.Services;
using Domain.Interfaces;
using FluentValidation.AspNetCore;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Shared.Settings;
using System.Linq;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog structured logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/app-.txt",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
        retainedFileCountLimit: 7
    )
    .CreateLogger();

try
{
    Log.Information("🚀 Application starting up...");

    builder.Host.UseSerilog();

    // Add services to the container
    builder.Services.AddControllers()
        .AddFluentValidation(config =>
        {
            config.RegisterValidatorsFromAssemblyContaining<Program>();
            config.DisableDataAnnotationsValidation = false;
        });
    builder.Services.AddHealthChecks();

    // Configure DbContext
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? builder.Configuration["DbConnectionString"]
        ?? throw new InvalidOperationException("Connection string not found");

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(connectionString, npgsql =>
            npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

    // Add Swagger
    builder.Services.AddSwaggerGen();

    // Configure JWT Settings
    builder.Services.AddOptions<JwtSettings>()
        .Bind(builder.Configuration.GetSection("Jwt"))
        .Validate(settings => !string.IsNullOrWhiteSpace(settings.Secret), "JWT Secret is required.")
        .Validate(settings => settings.Secret.Length >= 32, "JWT Secret must be at least 32 characters long.")
        .Validate(settings => !string.IsNullOrWhiteSpace(settings.Issuer), "JWT Issuer is required.")
        .Validate(settings => !string.IsNullOrWhiteSpace(settings.Audience), "JWT Audience is required.")
        .Validate(settings => settings.AccessTokenExpiresInMinutes > 0, "AccessTokenExpiresInMinutes must be greater than 0.")
        .Validate(settings => settings.RefreshTokenExpiresInDays > 0, "RefreshTokenExpiresInDays must be greater than 0.")
        .Validate(settings => !string.IsNullOrWhiteSpace(settings.RefreshTokenCookieName), "RefreshTokenCookieName is required.")
        .Validate(settings => !string.IsNullOrWhiteSpace(settings.RefreshTokenCookiePath), "RefreshTokenCookiePath is required.")
        .Validate(settings => Enum.TryParse<SameSiteMode>(settings.RefreshTokenCookieSameSite, true, out _), "RefreshTokenCookieSameSite must be one of: Strict, Lax, None, Unspecified.")
        .Validate(settings => Enum.TryParse<CookieSecurePolicy>(settings.RefreshTokenCookieSecurePolicy, true, out _), "RefreshTokenCookieSecurePolicy must be one of: Always, SameAsRequest, None.")
        .Validate(settings => !string.Equals(settings.RefreshTokenCookieSameSite, "None", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(settings.RefreshTokenCookieSecurePolicy, "None", StringComparison.OrdinalIgnoreCase),
            "Refresh token cookies with SameSite=None must not use CookieSecurePolicy=None.")
        .ValidateOnStart(); // Validate on application startup

    builder.Services.AddOptions<CorsSettings>()
        .Bind(builder.Configuration.GetSection("Cors"))
        .Validate(settings => settings.AllowedOrigins.Length > 0, "At least one CORS allowed origin is required.")
        .Validate(settings => settings.AllowedOrigins.All(origin => Uri.TryCreate(origin, UriKind.Absolute, out _)),
            "All CORS allowed origins must be absolute URLs.")
        .Validate(settings => !settings.AllowCredentials || settings.AllowedOrigins.All(origin => origin != "*"),
            "Wildcard CORS origins cannot be used when credentials are enabled.")
        .ValidateOnStart();

    builder.Services.AddOptions<EmailConfirmationSettings>()
        .Bind(builder.Configuration.GetSection("EmailConfirmation"))
        .Validate(settings => Uri.TryCreate(settings.PublicOrigin, UriKind.Absolute, out _),
            "Email confirmation public origin must be an absolute URL.")
        .Validate(settings => settings.TokenExpiresInHours > 0,
            "Email confirmation token lifetime must be greater than 0 hours.")
        .Validate(settings => !string.IsNullOrWhiteSpace(settings.ConfirmationPath),
            "Email confirmation path is required.")
        .ValidateOnStart();

    builder.Services.AddOptions<EmailTwoFactorSettings>()
        .Bind(builder.Configuration.GetSection("EmailTwoFactor"))
        .Validate(settings => settings.CodeExpiresInMinutes > 0,
            "Email 2FA code lifetime must be greater than 0 minutes.")
        .Validate(settings => settings.CodeLength >= 4 && settings.CodeLength <= 10,
            "Email 2FA code length must be between 4 and 10 digits.")
        .Validate(settings => settings.MaxFailedAttempts > 0,
            "Email 2FA maximum failed attempts must be greater than 0.")
        .ValidateOnStart();

    builder.Services.AddOptions<EmailDeliverySettings>()
        .Bind(builder.Configuration.GetSection("EmailDelivery"))
        .Validate(settings => !settings.Enabled || !string.IsNullOrWhiteSpace(settings.Host),
            "Email delivery host is required when email delivery is enabled.")
        .Validate(settings => !settings.Enabled || settings.Port > 0,
            "Email delivery port must be greater than 0 when email delivery is enabled.")
        .Validate(settings => !settings.Enabled || !string.IsNullOrWhiteSpace(settings.FromAddress),
            "Email delivery from address is required when email delivery is enabled.")
        .ValidateOnStart();

    var corsSettings = builder.Configuration.GetSection("Cors").Get<CorsSettings>() ?? new CorsSettings();
    var emailDeliverySettings = builder.Configuration.GetSection("EmailDelivery").Get<EmailDeliverySettings>() ?? new EmailDeliverySettings();


    builder.Services.AddHttpContextAccessor();
    builder.Services.AddHealthChecks();
    // Configure JWT Authentication
    var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>();
    if (jwtSettings == null)
        throw new InvalidOperationException("JWT settings not configured in appsettings.json");

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret))
        };
    });

    // Register services
    builder.Services.AddScoped<ValidationFilterAttribute>();
    builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
    builder.Services.AddScoped<IAuthService, DatabaseAuthService>();
    builder.Services.AddScoped<Application.Interfaces.IUserService, DatabaseUserService>();
    builder.Services.AddScoped<LoggingAccountEmailSender>();
    builder.Services.AddScoped<MailKitAccountEmailSender>();
    builder.Services.AddScoped<IAccountEmailSender>(serviceProvider =>
        emailDeliverySettings.Enabled
            ? serviceProvider.GetRequiredService<MailKitAccountEmailSender>()
            : serviceProvider.GetRequiredService<LoggingAccountEmailSender>());

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddFixedWindowLimiter("AuthPolicy", limiterOptions =>
        {
            limiterOptions.PermitLimit = 5;
            limiterOptions.Window = TimeSpan.FromMinutes(1);
            limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            limiterOptions.QueueLimit = 0;
        });
    });


    // Configure CORS
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("ReactApp", policy =>
        {
            policy.WithOrigins(corsSettings.AllowedOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader();

            if (corsSettings.AllowCredentials)
            {
                policy.AllowCredentials();
            }
        });
    });

    var app = builder.Build();

    Log.Information("📊 Configuring application middleware...");

    // Apply migrations and seed data automatically
    using (var scope = app.Services.CreateScope())
    {
        try
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            if (dbContext.Database.IsRelational())
            {
                // Apply pending Entity Framework migrations only for relational providers.
                await dbContext.Database.MigrateAsync();
                Log.Information("✓ Database initialized successfully!");
            }
            else
            {
                Log.Information("✓ Database initialized using non-relational provider");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "✗ Database initialization failed");
        }
    }

    // Configure the HTTP request pipeline
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

    app.MapHealthChecks("/health");
    app.MapControllers();

    Log.Information("🌐 Application listening on configured ports");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "💥 Application terminated unexpectedly");
}
finally
{
    Log.Information("🛑 Application shutting down...");
    await Log.CloseAndFlushAsync();
}
