using Domain.Enums;

namespace Application.DTOs.Project;

public class ProjectTaskDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ProjectTaskStatus Status { get; set; }
    public ProjectTaskPriority Priority { get; set; }
    public DateTime? DueDate { get; set; }
    public Guid? AssignedUserId { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}