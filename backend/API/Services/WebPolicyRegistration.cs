using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Responses;
using Shared.Settings;
using System.Threading.RateLimiting;

namespace API.Services;

public static partial class ProjectServiceCollectionExtensions
{
        private static IServiceCollection AddCorsPolicy(this IServiceCollection services, IConfiguration configuration)
        {
            var corsSettings = configuration.GetSection("Cors").Get<CorsSettings>() ?? new CorsSettings();

            services.AddCors(options =>
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

            return services;
        }

        private static IServiceCollection AddRateLimitingInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            var authSecuritySettings = configuration.GetSection("AuthSecurity").Get<AuthSecuritySettings>() ?? new AuthSecuritySettings();

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = static async (context, cancellationToken) =>
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.HttpContext.Response.ContentType = "application/json";
                    await context.HttpContext.Response.WriteAsJsonAsync(
                        ApiResponse.Error(
                            StatusCodes.Status429TooManyRequests,
                            "Too many requests. Please try again later."),
                        cancellationToken);
                };

                options.AddPolicy("AuthPolicy", httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        GetAuthRateLimitPartitionKey(httpContext),
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = authSecuritySettings.RateLimitPermitLimit,
                            Window = TimeSpan.FromSeconds(authSecuritySettings.RateLimitWindowSeconds),
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = 0
                        }));
            });

            return services;
        }

}
