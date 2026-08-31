using Application.Modules.Projects.AcceptProjectInvitation;
using Application.Modules.Projects.AddProjectMember;
using Application.Modules.Projects.ArchiveProject;
using Application.Modules.Projects.ChangeProjectMemberRole;
using Application.Modules.Projects.CreateProject;
using Application.Modules.Projects.CreateProjectInvitation;
using Application.Modules.Projects.DeclineProjectInvitation;
using Application.Modules.Projects.GetProjectDetails;
using Application.Modules.Projects.GetProjectActivity;
using Application.Modules.Projects.GetProjectDashboard;
using Application.Modules.Projects.Invitations;
using Application.Modules.Projects.ListAvailableProjectMembers;
using Application.Modules.Projects.ListMyProjectInvitations;
using Application.Modules.Projects.ListProjectMembers;
using Application.Modules.Projects.ListProjectInvitations;
using Application.Modules.Projects.ListProjects;
using Application.Modules.Projects.RemoveProjectMember;
using Application.Modules.Projects.UpdateProject;
using Infrastructure.Modules.Projects.AcceptProjectInvitation;
using Infrastructure.Modules.Projects.AddProjectMember;
using Infrastructure.Modules.Projects.ArchiveProject;
using Infrastructure.Modules.Projects.ChangeProjectMemberRole;
using Infrastructure.Modules.Projects.CreateProject;
using Infrastructure.Modules.Projects.CreateProjectInvitation;
using Infrastructure.Modules.Projects.DeclineProjectInvitation;
using Infrastructure.Modules.Projects.GetProjectDetails;
using Infrastructure.Modules.Projects.GetProjectActivity;
using Infrastructure.Modules.Projects.GetProjectDashboard;
using Infrastructure.Modules.Projects.Invitations;
using Infrastructure.Modules.Projects.ListAvailableProjectMembers;
using Infrastructure.Modules.Projects.ListMyProjectInvitations;
using Infrastructure.Modules.Projects.ListProjectMembers;
using Infrastructure.Modules.Projects.ListProjectInvitations;
using Infrastructure.Modules.Projects.ListProjects;
using Infrastructure.Modules.Projects.RemoveProjectMember;
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
        services.AddScoped<IAddProjectMemberStore, EfAddProjectMemberStore>();
        services.AddScoped<IAddProjectMemberNotificationWriter, EfAddProjectMemberNotificationWriter>();
        services.AddScoped<IAddProjectMemberHandler, AddProjectMemberHandler>();
        services.AddScoped<IArchiveProjectStore, EfArchiveProjectStore>();
        services.AddScoped<IArchiveProjectHandler, ArchiveProjectHandler>();
        services.AddScoped<IChangeProjectMemberRoleStore, EfChangeProjectMemberRoleStore>();
        services.AddScoped<IChangeProjectMemberRoleHandler, ChangeProjectMemberRoleHandler>();
        services.AddScoped<ICreateProjectStore, EfCreateProjectStore>();
        services.AddScoped<ICreateProjectHandler, CreateProjectHandler>();
        services.AddScoped<IGetProjectDetailsStore, EfGetProjectDetailsStore>();
        services.AddScoped<IGetProjectDetailsHandler, GetProjectDetailsHandler>();
        services.AddScoped<IGetProjectActivityStore, EfGetProjectActivityStore>();
        services.AddScoped<IGetProjectActivityHandler, GetProjectActivityHandler>();
        services.AddScoped<IGetProjectDashboardStore, EfGetProjectDashboardStore>();
        services.AddScoped<IGetProjectDashboardHandler, GetProjectDashboardHandler>();
        services.AddScoped<IListAvailableProjectMembersStore, EfListAvailableProjectMembersStore>();
        services.AddScoped<IListAvailableProjectMembersHandler, ListAvailableProjectMembersHandler>();
        services.AddScoped<IListProjectsStore, EfListProjectsStore>();
        services.AddScoped<IListProjectsHandler, ListProjectsHandler>();
        services.AddScoped<IListProjectMembersStore, EfListProjectMembersStore>();
        services.AddScoped<IListProjectMembersHandler, ListProjectMembersHandler>();
        services.AddScoped<IRemoveProjectMemberStore, EfRemoveProjectMemberStore>();
        services.AddScoped<IRemoveProjectMemberHandler, RemoveProjectMemberHandler>();
        services.AddScoped<IListProjectInvitationsStore, EfListProjectInvitationsStore>();
        services.AddScoped<IListProjectInvitationsHandler, ListProjectInvitationsHandler>();
        services.AddScoped<IListMyProjectInvitationsStore, EfListMyProjectInvitationsStore>();
        services.AddScoped<IListMyProjectInvitationsHandler, ListMyProjectInvitationsHandler>();
        services.AddScoped<ICreateProjectInvitationStore, EfCreateProjectInvitationStore>();
        services.AddScoped<IProjectInvitationNotificationWriter, EfProjectInvitationNotificationWriter>();
        services.AddScoped<ICreateProjectInvitationHandler, CreateProjectInvitationHandler>();
        services.AddScoped<IProjectInvitationResponseStore, EfProjectInvitationResponseStore>();
        services.AddScoped<IAcceptProjectInvitationHandler, AcceptProjectInvitationHandler>();
        services.AddScoped<IDeclineProjectInvitationHandler, DeclineProjectInvitationHandler>();
        services.AddScoped<IUpdateProjectStore, EfUpdateProjectStore>();
        services.AddScoped<IUpdateProjectHandler, UpdateProjectHandler>();

        return services;
    }
}
