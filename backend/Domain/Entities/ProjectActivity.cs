namespace Domain.Entities;

public sealed class ProjectActivity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid ActorUserId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? ProjectTaskId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Project Project { get; set; } = null!;
    public User ActorUser { get; set; } = null!;
    public ProjectTask? ProjectTask { get; set; }
}