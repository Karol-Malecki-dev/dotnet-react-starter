namespace Application.Features.Projects;

public interface IProjectApplicationService :
    IProjectManagementService,
    IProjectMembershipApplicationService,
    IProjectInvitationApplicationService
{
}
