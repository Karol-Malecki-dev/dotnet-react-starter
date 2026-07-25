using Domain.Enums;

namespace Application.DTOs.Project;

public class UpdateProjectTaskDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ProjectTaskPriority Priority { get; set; } = ProjectTaskPriority.Normal;
    public DateTime? DueDate { get; set; }
    public Guid? AssignedUserId { get; set; }
}