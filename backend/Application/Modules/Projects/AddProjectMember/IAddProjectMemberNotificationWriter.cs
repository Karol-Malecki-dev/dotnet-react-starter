namespace Application.Modules.Projects.AddProjectMember;

/// <summary>
/// Stages the notification produced when a user is added to a project.
/// Implementations must not commit the current unit of work.
/// </summary>
public interface IAddProjectMemberNotificationWriter
{
    Task AddProjectMemberNotificationAsync(
        Guid userId,
        Guid projectId,
        string projectName,
        CancellationToken cancellationToken = default);
}
