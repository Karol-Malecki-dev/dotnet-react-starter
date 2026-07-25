using Domain.Enums;

namespace Domain.Entities;

public class ProjectMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid UserId { get; set; }
    public ProjectMemberRole Role { get; set; } = ProjectMemberRole.Member;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public Project Project { get; set; } = null!;
    public User User { get; set; } = null!;
}