namespace Domain.Entities;

public class ProjectTaskLabel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectTaskId { get; set; }
    public string Name { get; set; } = string.Empty;

    public ProjectTask ProjectTask { get; set; } = null!;
}