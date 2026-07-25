namespace Application.DTOs.Project;

using Domain.Enums;

public class ProjectMemberDto
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public ProjectMemberRole Role { get; set; }
    public DateTime AddedAt { get; set; }
}