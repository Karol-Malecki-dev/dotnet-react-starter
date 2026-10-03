using API.Filters;
using FluentValidation;
using FluentValidation.AspNetCore;
using Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Shared.Responses;

namespace API.Services;

public static partial class ProjectServiceCollectionExtensions
{
        private static IServiceCollection AddApiPipelineServices(this IServiceCollection services)
        {
            services.AddControllers()
                .ConfigureApiBehaviorOptions(options =>
                {
                    options.InvalidModelStateResponseFactory = context =>
                        new BadRequestObjectResult(ValidationResponseFactory.Create(context.ModelState));
                });
            services.AddFluentValidationAutoValidation();
            services.AddValidatorsFromAssemblyContaining<global::Program>();

            services.AddHealthChecks()
                .AddCheck<DatabaseHealthCheck>("database", tags: ["ready", "database"])
                .AddCheck<BackgroundWorkerHealthCheck>("background-workers", tags: ["workers"])
                .AddCheck<AttachmentStorageHealthCheck>("attachment-storage", tags: ["ready", "storage", "object-storage"])
                .AddCheck<AttachmentMalwareScannerHealthCheck>("attachment-malware-scanner", tags: ["ready", "storage", "malware-scanner"])
                .AddCheck<EmailDeliveryHealthCheck>("email-delivery", tags: ["email"]);
            services.AddHttpContextAccessor();
            services.AddSwaggerGen();
            services.AddAuthorization();

            return services;
        }

}
