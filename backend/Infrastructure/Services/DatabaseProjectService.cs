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

    public Task<ProjectOperationResult<List<ProjectView>>> GetUserProjectsAsync(Guid userId, bool includeArchived = false, string scope = "all", CancellationToken cancellationToken = default)
        => _managementService.GetUserProjectsAsync(userId, includeArchived, scope, cancellationToken);

    public Task<ProjectOperationResult<ProjectView>> GetProjectAsync(Guid userId, Guid projectId, bool includeArchived = false, CancellationToken cancellationToken = default)
        => _managementService.GetProjectAsync(userId, projectId, includeArchived, cancellationToken);

    public Task<ProjectOperationResult<ProjectView>> CreateProjectAsync(CreateProjectCommand command, CancellationToken cancellationToken = default)
        => _managementService.CreateProjectAsync(command, cancellationToken);

    public Task<ProjectOperationResult<ProjectView>> UpdateProjectAsync(UpdateProjectCommand command, CancellationToken cancellationToken = default)
        => _managementService.UpdateProjectAsync(command, cancellationToken);

    public Task<ProjectOperationResult<bool>> ArchiveProjectAsync(Guid ownerId, Guid projectId, CancellationToken cancellationToken = default)
        => _managementService.ArchiveProjectAsync(ownerId, projectId, cancellationToken);

    public Task<ProjectOperationResult<PagedProjectActivityView>> GetProjectActivitiesAsync(Guid userId, Guid projectId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        => _managementService.GetProjectActivitiesAsync(userId, projectId, pageNumber, pageSize, cancellationToken);

    public Task<ProjectOperationResult<ProjectDashboardView>> GetProjectDashboardAsync(Guid userId, Guid projectId, CancellationToken cancellationToken = default)
        => _managementService.GetProjectDashboardAsync(userId, projectId, cancellationToken);

    public Task<ProjectOperationResult<List<ProjectMemberView>>> GetProjectMembersAsync(Guid ownerId, Guid projectId, CancellationToken cancellationToken = default)
        => _membershipService.GetProjectMembersAsync(ownerId, projectId, cancellationToken);

    public Task<ProjectOperationResult<List<ProjectMemberUserView>>> GetAvailableProjectMembersAsync(Guid ownerId, Guid projectId, CancellationToken cancellationToken = default)
        => _membershipService.GetAvailableProjectMembersAsync(ownerId, projectId, cancellationToken);

    public Task<ProjectOperationResult<ProjectMemberView>> AddProjectMemberAsync(Guid ownerId, Guid projectId, Guid userId, CancellationToken cancellationToken = default)
        => _membershipService.AddProjectMemberAsync(ownerId, projectId, userId, cancellationToken);

    public Task<ProjectOperationResult<ProjectMemberView>> UpdateProjectMemberRoleAsync(Guid ownerId, Guid projectId, Guid userId, Domain.Enums.ProjectMemberRole role, CancellationToken cancellationToken = default)
        => _membershipService.UpdateProjectMemberRoleAsync(ownerId, projectId, userId, role, cancellationToken);

    public Task<ProjectOperationResult<bool>> RemoveProjectMemberAsync(Guid ownerId, Guid projectId, Guid userId, CancellationToken cancellationToken = default)
        => _membershipService.RemoveProjectMemberAsync(ownerId, projectId, userId, cancellationToken);

    public Task<ProjectOperationResult<CreatedProjectInvitationView>> CreateProjectInvitationAsync(CreateProjectInvitationCommand command, CancellationToken cancellationToken = default)
        => _invitationService.CreateProjectInvitationAsync(command, cancellationToken);

    public Task<ProjectOperationResult<IReadOnlyList<ProjectInvitationView>>> GetProjectInvitationsAsync(Guid ownerId, Guid projectId, CancellationToken cancellationToken = default)
        => _invitationService.GetProjectInvitationsAsync(ownerId, projectId, cancellationToken);

    public Task<ProjectOperationResult<IReadOnlyList<ProjectInvitationView>>> GetMyProjectInvitationsAsync(Guid userId, CancellationToken cancellationToken = default)
        => _invitationService.GetMyProjectInvitationsAsync(userId, cancellationToken);

    public Task<ProjectOperationResult<ProjectInvitationView>> AcceptProjectInvitationAsync(Guid userId, string token, CancellationToken cancellationToken = default)
        => _invitationService.AcceptProjectInvitationAsync(userId, token, cancellationToken);

    public Task<ProjectOperationResult<ProjectInvitationView>> DeclineProjectInvitationAsync(Guid userId, string token, CancellationToken cancellationToken = default)
        => _invitationService.DeclineProjectInvitationAsync(userId, token, cancellationToken);
}
