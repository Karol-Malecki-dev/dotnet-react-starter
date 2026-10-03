using API.Filters;
using API.Configurations;
using Application.Interfaces;
using Application.Services;
using Application.Modules.Workspace.SearchWorkspace;
using Domain.Interfaces;
using Infrastructure.Data;
using Infrastructure.Dispatching;
using Infrastructure.Modules.Notifications;
using Infrastructure.Modules.ProjectTasks;
using Infrastructure.Modules.Projects;
using Infrastructure.Modules.Workspace.SearchWorkspace;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shared.Settings;

namespace API.Services;

public static partial class ProjectServiceCollectionExtensions
{
        private static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? configuration["DbConnectionString"]
                ?? throw new InvalidOperationException("Connection string not found");

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(connectionString, npgsql =>
                    npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

            return services;
        }

        private static IServiceCollection AddAuthenticationInfrastructure(this IServiceCollection services)
        {
            services.AddSingleton<IConfigureOptions<JwtBearerOptions>, JwtBearerOptionsSetup>();

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer();

            return services;
        }

        private static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services.AddScoped<ValidationFilterAttribute>();

            services.AddScoped<IJwtTokenService, JwtTokenService>();
            services.AddScoped<IAuthService, DatabaseAuthService>();
            services.AddScoped<IUserService, DatabaseUserService>();
            services.AddScoped<INotificationWriter, DatabaseNotificationWriter>();
            services.AddScoped<ICollaborationNotificationWriter, CollaborationNotificationWriter>();
            services.AddScoped<LoggingNotificationEmailSender>();
            services.AddSingleton<BackgroundWorkerHealthState>();
            services.AddSingleton<EmailDeliveryHealthState>();
            services.AddScoped<MailKitNotificationEmailSender>();
            services.AddScoped<INotificationEmailSender>(serviceProvider =>
            {
                var emailDeliverySettings = serviceProvider.GetRequiredService<IOptions<EmailDeliverySettings>>().Value;
                return emailDeliverySettings.Enabled
                    ? serviceProvider.GetRequiredService<MailKitNotificationEmailSender>()
                    : serviceProvider.GetRequiredService<LoggingNotificationEmailSender>();
            });
            services.AddScoped<INotificationEmailOutboxProcessor, NotificationEmailOutboxProcessor>();
            services.AddScoped<INotificationEmailOutboxMetricsReader, NotificationEmailOutboxMetricsReader>();
            services.AddHostedService<NotificationEmailOutboxWorker>();
            services.AddScoped<IAdminService, DatabaseAdminService>();
            services.AddScoped<ISearchWorkspaceHandler, SearchWorkspaceHandler>();
            services.AddScoped<ISearchWorkspaceStore, EfSearchWorkspaceStore>();
            services.AddNotificationsModule();
            services.AddProjectTasksModule();
            services.AddProjectsModule();

            services.AddScoped<LoggingAccountEmailSender>();
            services.AddScoped<MailKitAccountEmailSender>();
            services.AddScoped<IAccountEmailSender>(serviceProvider =>
            {
                var emailDeliverySettings = serviceProvider.GetRequiredService<IOptions<EmailDeliverySettings>>().Value;

                return emailDeliverySettings.Enabled
                    ? serviceProvider.GetRequiredService<MailKitAccountEmailSender>()
                    : serviceProvider.GetRequiredService<LoggingAccountEmailSender>();
            });

            return services;
        }

}
