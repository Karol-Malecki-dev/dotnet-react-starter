using Application.Features.ProjectManagement.Tasks;
using Application.Interfaces;
using Application.Modules.ProjectTasks.Attachments;
using Application.Modules.ProjectTasks.Assignments;
using Application.Modules.ProjectTasks.AssignmentNotifications;
using Application.Modules.ProjectTasks.CreateProjectTask;
using Application.Modules.ProjectTasks.CreateProjectTaskComment;
using Application.Modules.ProjectTasks.CreateProjectTaskAttachment;
using Application.Modules.ProjectTasks.DeadlineReminders;
using Application.Modules.ProjectTasks.DeleteProjectTask;
using Application.Modules.ProjectTasks.DeleteProjectTaskComment;
using Application.Modules.ProjectTasks.Dashboard;
using Application.Modules.ProjectTasks.DeleteProjectTaskAttachment;
using Application.Modules.ProjectTasks.DownloadProjectTaskAttachment;
using Application.Modules.ProjectTasks.GetProjectTaskDetails;
using Application.Modules.ProjectTasks.ListProjectTasks;
using Application.Modules.ProjectTasks.ListProjectTaskComments;
using Application.Modules.ProjectTasks.ListProjectTaskAttachments;
using Application.Modules.ProjectTasks.UpdateProjectTask;
using Application.Modules.ProjectTasks.UpdateProjectTaskStatus;
using Infrastructure.ProjectManagement.Tasks;
using Infrastructure.Modules.ProjectTasks.CreateProjectTask;
using Infrastructure.Modules.ProjectTasks.CreateProjectTaskComment;
using Infrastructure.Modules.ProjectTasks.CreateProjectTaskAttachment;
using Infrastructure.Modules.ProjectTasks.AssignmentNotifications;
using Infrastructure.Modules.ProjectTasks.Assignments;
using Infrastructure.Modules.ProjectTasks.Attachments;
using Infrastructure.Modules.ProjectTasks.DeadlineReminders;
using Infrastructure.Modules.ProjectTasks.DeleteProjectTask;
using Infrastructure.Modules.ProjectTasks.DeleteProjectTaskComment;
using Infrastructure.Modules.ProjectTasks.Dashboard;
using Infrastructure.Modules.ProjectTasks.DeleteProjectTaskAttachment;
using Infrastructure.Modules.ProjectTasks.DownloadProjectTaskAttachment;
using Infrastructure.Modules.ProjectTasks.GetProjectTaskDetails;
using Infrastructure.Modules.ProjectTasks.ListProjectTasks;
using Infrastructure.Modules.ProjectTasks.ListProjectTaskComments;
using Infrastructure.Modules.ProjectTasks.ListProjectTaskAttachments;
using Infrastructure.Modules.ProjectTasks.UpdateProjectTask;
using Infrastructure.Modules.ProjectTasks.UpdateProjectTaskStatus;
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
        services.AddScoped<IProjectTaskDeadlineReminderProcessor, ProjectTaskDeadlineReminderProcessor>();
        services.AddHostedService<ProjectTaskDeadlineReminderWorker>();
        services.AddScoped<IProjectTaskAttachmentCleanupProcessor, ProjectTaskAttachmentCleanupProcessor>();
        services.AddHostedService<ProjectTaskAttachmentCleanupWorker>();

        services.AddScoped<IProjectTaskAccess, EfProjectTaskAccess>();
        services.AddScoped<IProjectTaskAssignmentNotificationWriter, EfProjectTaskAssignmentNotificationWriter>();
        services.AddScoped<IProjectTaskMemberAssignmentWriter, EfProjectTaskMemberAssignmentWriter>();
        services.AddScoped<IListProjectTasksQueryStore, EfProjectTaskQueryStore>();
        services.AddScoped<IProjectTaskCommandStore, EfProjectTaskCommandStore>();
        services.AddScoped<IProjectTaskAttachmentCleanupQueue, EfProjectTaskAttachmentCleanupQueue>();
        services.AddScoped<IListProjectTaskCommentsQueryStore, EfListProjectTaskCommentsQueryStore>();
        services.AddScoped<ICreateProjectTaskCommentStore, EfCreateProjectTaskCommentStore>();
        services.AddScoped<IDeleteProjectTaskCommentStore, EfDeleteProjectTaskCommentStore>();
        services.AddScoped<IListProjectTaskAttachmentsQueryStore, EfListProjectTaskAttachmentsQueryStore>();
        services.AddScoped<ICreateProjectTaskAttachmentStore, EfCreateProjectTaskAttachmentStore>();
        services.AddScoped<IDownloadProjectTaskAttachmentStore, EfDownloadProjectTaskAttachmentStore>();
        services.AddScoped<IDeleteProjectTaskAttachmentStore, EfDeleteProjectTaskAttachmentStore>();
        services.AddSingleton<IProjectTaskAttachmentStorage, LocalProjectTaskAttachmentStorage>();
        services.AddScoped<ICreateProjectTaskHandler, CreateProjectTaskHandler>();
        services.AddScoped<ICreateProjectTaskAttachmentHandler, CreateProjectTaskAttachmentHandler>();
        services.AddScoped<ICreateProjectTaskCommentHandler, CreateProjectTaskCommentHandler>();
        services.AddScoped<IDeleteProjectTaskHandler, DeleteProjectTaskHandler>();
        services.AddScoped<IDeleteProjectTaskAttachmentHandler, DeleteProjectTaskAttachmentHandler>();
        services.AddScoped<IDeleteProjectTaskCommentHandler, DeleteProjectTaskCommentHandler>();
        services.AddScoped<IProjectTaskDashboardReader, EfProjectTaskDashboardReader>();
        services.AddScoped<IDownloadProjectTaskAttachmentHandler, DownloadProjectTaskAttachmentHandler>();
        services.AddScoped<IGetProjectTaskDetailsHandler, GetProjectTaskDetailsHandler>();
        services.AddScoped<IListProjectTasksHandler, ListProjectTasksHandler>();
        services.AddScoped<IListProjectTaskAttachmentsHandler, ListProjectTaskAttachmentsHandler>();
        services.AddScoped<IListProjectTaskCommentsHandler, ListProjectTaskCommentsHandler>();
        services.AddScoped<IUpdateProjectTaskHandler, UpdateProjectTaskHandler>();
        services.AddScoped<IUpdateProjectTaskStatusHandler, UpdateProjectTaskStatusHandler>();

        return services;
    }
}
