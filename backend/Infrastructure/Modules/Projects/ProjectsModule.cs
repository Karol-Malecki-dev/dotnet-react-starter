using Application.Modules.Projects.ArchiveProject;
using Application.Modules.Projects.CreateProject;
using Application.Modules.Projects.GetProjectDetails;
using Application.Modules.Projects.ListProjects;
using Application.Modules.Projects.ListProjectMembers;
using Application.Modules.Projects.UpdateProject;
using Infrastructure.Modules.Projects.ArchiveProject;
using Infrastructure.Modules.Projects.CreateProject;
using Infrastructure.Modules.Projects.GetProjectDetails;
using Infrastructure.Modules.Projects.ListProjects;
using Infrastructure.Modules.Projects.ListProjectMembers;
using Infrastructure.Modules.Projects.UpdateProject;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Modules.Projects;

/// <summary>
/// Registers the Projects module and its migrated vertical slices.
/// </summary>
public static class ProjectsModule
{
    public static IServiceCollection AddProjectsModule(this IServiceCollection services)
    {
        services.AddScoped<IArchiveProjectStore, EfArchiveProjectStore>();
        services.AddScoped<IArchiveProjectHandler, ArchiveProjectHandler>();
        services.AddScoped<ICreateProjectStore, EfCreateProjectStore>();
        services.AddScoped<ICreateProjectHandler, CreateProjectHandler>();
        services.AddScoped<IGetProjectDetailsStore, EfGetProjectDetailsStore>();
        services.AddScoped<IGetProjectDetailsHandler, GetProjectDetailsHandler>();
        services.AddScoped<IListProjectsStore, EfListProjectsStore>();
        services.AddScoped<IListProjectsHandler, ListProjectsHandler>();
        services.AddScoped<IListProjectMembersStore, EfListProjectMembersStore>();
        services.AddScoped<IListProjectMembersHandler, ListProjectMembersHandler>();
        services.AddScoped<IUpdateProjectStore, EfUpdateProjectStore>();
        services.AddScoped<IUpdateProjectHandler, UpdateProjectHandler>();

        return services;
    }
}
