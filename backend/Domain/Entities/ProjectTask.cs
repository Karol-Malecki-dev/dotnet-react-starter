using Domain.Enums;

namespace Domain.Entities;

public class ProjectTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ProjectTaskStatus Status { get; set; } = ProjectTaskStatus.Todo;
    public ProjectTaskPriority Priority { get; set; } = ProjectTaskPriority.Normal;
    public DateTime? DueDate { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public Guid? AssignedUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Project Project { get; set; } = null!;
    public User? CreatedByUser { get; set; }
    public User? AssignedUser { get; set; }
    public ICollection<ProjectTaskComment> Comments { get; set; } = [];
    public ICollection<ProjectTaskAttachment> Attachments { get; set; } = [];
    public ICollection<ProjectTaskLabel> Labels { get; set; } = [];
}