using Application.Features.ProjectManagement.Tasks;
using Application.Interfaces;
using Application.Modules.ProjectTasks.CreateProjectTask;
using Infrastructure.ProjectManagement.Tasks;
using Infrastructure.Modules.ProjectTasks.CreateProjectTask;
using Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Modules.ProjectTasks;

/// <summary>
/// Registers the ProjectTasks module and its current application, persistence, and worker adapters.
/// </summary>
public static class ProjectTasksModule
{
    public static IServiceCollection AddProjectTasksModule(this IServiceCollection services)
    {
        services.AddScoped<IProjectTaskDeadlineReminderService, ProjectTaskDeadlineReminderService>();
        services.AddHostedService<ProjectTaskDeadlineReminderWorker>();

        services.AddScoped<IProjectTaskAccess, EfProjectTaskAccess>();
        services.AddScoped<IProjectTaskQueryStore, EfProjectTaskQueryStore>();
        services.AddScoped<IProjectTaskCommandStore, EfProjectTaskCommandStore>();
        services.AddScoped<IProjectTaskQueryService, DatabaseProjectTaskQueryService>();
        services.AddScoped<IProjectTaskCommandService, DatabaseProjectTaskCommandService>();
        services.AddScoped<IProjectTaskCommentApplicationService, DatabaseProjectTaskCommentService>();
        services.AddSingleton<IProjectTaskAttachmentStorage, LocalProjectTaskAttachmentStorage>();
        services.AddScoped<IProjectTaskAttachmentApplicationService, DatabaseProjectTaskAttachmentService>();
        services.AddScoped<ICreateProjectTaskHandler, CreateProjectTaskHandler>();

        return services;
    }
}
