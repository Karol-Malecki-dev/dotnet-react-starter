namespace Application.Modules.ProjectTasks.Assignments;

/// <summary>
/// Stages assignment changes required when a user leaves a project.
/// Implementations must participate in the current unit of work and must not commit it.
/// </summary>
public interface IProjectTaskMemberAssignmentWriter
{
    Task UnassignAllAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
