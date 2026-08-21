using Application.Features.Projects;
using Application.Interfaces;
using Infrastructure.Data;

namespace Infrastructure.Services;

public sealed class DatabaseProjectService :
    IProjectApplicationService,
    IProjectManagementService,
    IProjectMembershipApplicationService,
    IProjectInvitationApplicationService
{
    private readonly IProjectManagementService _managementService;
    private readonly IProjectMembershipApplicationService _membershipService;
    private readonly IProjectInvitationApplicationService _invitationService;

    public DatabaseProjectService(
        ApplicationDbContext dbContext,
        IProjectMembershipStore membershipStore,
        IProjectInvitationStore invitationStore,
        INotificationService notificationService)
    {
        _managementService = new DatabaseProjectManagementService(dbContext, membershipStore);
        _membershipService = new DatabaseProjectMembershipApplicationService(dbContext, membershipStore, notificationService);
        _invitationService = new DatabaseProjectInvitationApplicationService(membershipStore, invitationStore, notificationService);
    }

    public Task<ProjectOperationResult<List<ProjectView>>> GetUserProjectsAsync(Guid userId, bool includeArchived = false, string scope = "all")
        => _managementService.GetUserProjectsAsync(userId, includeArchived, scope);

    public Task<ProjectOperationResult<ProjectView>> GetProjectAsync(Guid userId, Guid projectId, bool includeArchived = false)
        => _managementService.GetProjectAsync(userId, projectId, includeArchived);

    public Task<ProjectOperationResult<ProjectView>> CreateProjectAsync(CreateProjectCommand command)
        => _managementService.CreateProjectAsync(command);

    public Task<ProjectOperationResult<ProjectView>> UpdateProjectAsync(UpdateProjectCommand command)
        => _managementService.UpdateProjectAsync(command);

    public Task<ProjectOperationResult<bool>> ArchiveProjectAsync(Guid ownerId, Guid projectId)
        => _managementService.ArchiveProjectAsync(ownerId, projectId);

    public Task<ProjectOperationResult<PagedProjectActivityView>> GetProjectActivitiesAsync(Guid userId, Guid projectId, int pageNumber, int pageSize)
        => _managementService.GetProjectActivitiesAsync(userId, projectId, pageNumber, pageSize);

    public Task<ProjectOperationResult<ProjectDashboardView>> GetProjectDashboardAsync(Guid userId, Guid projectId)
        => _managementService.GetProjectDashboardAsync(userId, projectId);

    public Task<ProjectOperationResult<List<ProjectMemberView>>> GetProjectMembersAsync(Guid ownerId, Guid projectId)
        => _membershipService.GetProjectMembersAsync(ownerId, projectId);

    public Task<ProjectOperationResult<List<ProjectMemberUserView>>> GetAvailableProjectMembersAsync(Guid ownerId, Guid projectId)
        => _membershipService.GetAvailableProjectMembersAsync(ownerId, projectId);

    public Task<ProjectOperationResult<ProjectMemberView>> AddProjectMemberAsync(Guid ownerId, Guid projectId, Guid userId)
        => _membershipService.AddProjectMemberAsync(ownerId, projectId, userId);

    public Task<ProjectOperationResult<ProjectMemberView>> UpdateProjectMemberRoleAsync(Guid ownerId, Guid projectId, Guid userId, Domain.Enums.ProjectMemberRole role)
        => _membershipService.UpdateProjectMemberRoleAsync(ownerId, projectId, userId, role);

    public Task<ProjectOperationResult<bool>> RemoveProjectMemberAsync(Guid ownerId, Guid projectId, Guid userId)
        => _membershipService.RemoveProjectMemberAsync(ownerId, projectId, userId);

    public Task<ProjectOperationResult<CreatedProjectInvitationView>> CreateProjectInvitationAsync(CreateProjectInvitationCommand command)
        => _invitationService.CreateProjectInvitationAsync(command);

    public Task<ProjectOperationResult<IReadOnlyList<ProjectInvitationView>>> GetProjectInvitationsAsync(Guid ownerId, Guid projectId)
        => _invitationService.GetProjectInvitationsAsync(ownerId, projectId);

    public Task<ProjectOperationResult<IReadOnlyList<ProjectInvitationView>>> GetMyProjectInvitationsAsync(Guid userId)
        => _invitationService.GetMyProjectInvitationsAsync(userId);

    public Task<ProjectOperationResult<ProjectInvitationView>> AcceptProjectInvitationAsync(Guid userId, string token)
        => _invitationService.AcceptProjectInvitationAsync(userId, token);

    public Task<ProjectOperationResult<ProjectInvitationView>> DeclineProjectInvitationAsync(Guid userId, string token)
        => _invitationService.DeclineProjectInvitationAsync(userId, token);
}
