using Application.Features.Projects;
using Domain.Entities;
using Domain.Enums;

namespace Infrastructure.Modules.Projects.Invitations;

internal static class ProjectInvitationViewMapper
{
    public static ProjectInvitationView Map(ProjectInvitation invitation)
        => new(
            invitation.Id,
            invitation.ProjectId,
            invitation.Project.Name,
            invitation.InvitedUserId,
            invitation.InvitedUser.DisplayName.Value,
            invitation.InvitedUser.Email.Value,
            invitation.InvitedByUser.DisplayName.Value,
            invitation.Role,
            invitation.Status == ProjectInvitationStatus.Pending
                && invitation.ExpiresAt <= DateTime.UtcNow
                    ? ProjectInvitationStatus.Expired
                    : invitation.Status,
            invitation.ExpiresAt,
            invitation.CreatedAt);
}
