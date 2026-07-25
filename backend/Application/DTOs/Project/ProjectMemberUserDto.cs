namespace Application.DTOs.Project;

public class ProjectMemberUserDto
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}