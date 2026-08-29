using Domain.Enums;

namespace Domain.Entities;

public class ProjectMember
{
    private ProjectMember()
    {
    }

    private ProjectMember(Guid projectId, Guid userId, ProjectMemberRole role)
    {
        Id = Guid.NewGuid();
        ProjectId = RequireIdentifier(projectId, nameof(projectId));
        UserId = RequireIdentifier(userId, nameof(userId));
        Role = role;
        AddedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid UserId { get; private set; }
    public ProjectMemberRole Role { get; private set; }
    public DateTime AddedAt { get; private set; }

    public Project Project { get; private set; } = null!;
    public User User { get; private set; } = null!;

    public static ProjectMember Create(Guid projectId, Guid userId, ProjectMemberRole role = ProjectMemberRole.Member)
    {
        if (role == ProjectMemberRole.Owner)
        {
            throw new ArgumentException("Owner membership is created by the project aggregate.", nameof(role));
        }

        EnsureDefinedRole(role);
        return new ProjectMember(projectId, userId, role);
    }

    internal static ProjectMember CreateOwner(Guid projectId, Guid userId)
        => new(projectId, userId, ProjectMemberRole.Owner);

    public void ChangeRole(ProjectMemberRole role)
    {
        if (role == ProjectMemberRole.Owner)
        {
            throw new ArgumentException("A project member cannot be assigned the owner role.", nameof(role));
        }

        EnsureDefinedRole(role);
        Role = role;
    }

    private static Guid RequireIdentifier(Guid identifier, string parameterName)
    {
        if (identifier == Guid.Empty)
        {
            throw new ArgumentException("Identifier is required.", parameterName);
        }

        return identifier;
    }

    private static void EnsureDefinedRole(ProjectMemberRole role)
    {
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown project member role.");
        }
    }
}