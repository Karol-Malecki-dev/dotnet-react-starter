using Application.Modules.Projects.GetProjectDetails;
using Infrastructure.Modules.Projects.GetProjectDetails;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Modules.Projects;

/// <summary>
/// Registers the Projects module and its migrated vertical slices.
/// </summary>
public static class ProjectsModule
{
    public static IServiceCollection AddProjectsModule(this IServiceCollection services)
    {
        services.AddScoped<IGetProjectDetailsStore, EfGetProjectDetailsStore>();
        services.AddScoped<IGetProjectDetailsHandler, GetProjectDetailsHandler>();

        return services;
    }
}
