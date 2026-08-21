using Domain.Enums;

namespace Domain.Entities;

public sealed class ProjectTask
{
    private readonly List<ProjectTaskLabel> _labels = [];

    private ProjectTask()
    {
    }

    private ProjectTask(Guid projectId, string title, string? description, ProjectTaskPriority priority, DateTime? dueDate, Guid? assignedUserId, Guid? createdByUserId)
    {
        Id = Guid.NewGuid();
        ProjectId = RequireIdentifier(projectId, nameof(projectId));
        Title = NormalizeTitle(title);
        Description = NormalizeDescription(description);
        Priority = RequireDefinedPriority(priority);
        DueDate = dueDate;
        AssignedUserId = assignedUserId;
        CreatedByUserId = createdByUserId;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public ProjectTaskStatus Status { get; private set; } = ProjectTaskStatus.Todo;
    public ProjectTaskPriority Priority { get; private set; } = ProjectTaskPriority.Normal;
    public DateTime? DueDate { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public Guid? AssignedUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public User? CreatedByUser { get; private set; }
    public User? AssignedUser { get; private set; }
    public IReadOnlyCollection<ProjectTaskLabel> Labels => _labels;
    public ICollection<ProjectTaskComment> Comments { get; private set; } = [];
    public ICollection<ProjectTaskAttachment> Attachments { get; private set; } = [];

    public static ProjectTask Create(
        Guid projectId,
        string title,
        string? description,
        ProjectTaskPriority priority,
        DateTime? dueDate,
        Guid? assignedUserId,
        Guid? createdByUserId,
        IEnumerable<string>? labels = null)
    {
        var task = new ProjectTask(projectId, title, description, priority, dueDate, assignedUserId, createdByUserId);
        task.ReplaceLabels(labels);
        return task;
    }

    public void Rename(string title)
    {
        Title = NormalizeTitle(title);
        Touch();
    }

    public void ChangeDescription(string? description)
    {
        Description = NormalizeDescription(description);
        Touch();
    }

    public void ChangePriority(ProjectTaskPriority priority)
    {
        Priority = RequireDefinedPriority(priority);
        Touch();
    }

    public void ChangeStatus(ProjectTaskStatus status)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), "Project task status is invalid.");
        }

        Status = status;
        Touch();
    }

    public void AssignTo(Guid userId)
    {
        AssignedUserId = RequireIdentifier(userId, nameof(userId));
        Touch();
    }

    public void Unassign()
    {
        AssignedUserId = null;
        Touch();
    }

    public void SetDueDate(DateTime? dueDate)
    {
        DueDate = dueDate;
        Touch();
    }

    public IReadOnlyList<string> ReplaceLabels(IEnumerable<string>? labels)
    {
        var normalizedLabels = (labels ?? [])
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Select(label => label.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(label => label, StringComparer.Ordinal)
            .Take(10)
            .ToList();

        _labels.Clear();
        _labels.AddRange(normalizedLabels.Select(label => new ProjectTaskLabel
        {
            ProjectTaskId = Id,
            Name = label
        }));
        Touch();
        return normalizedLabels;
    }

    private static Guid RequireIdentifier(Guid identifier, string parameterName)
    {
        if (identifier == Guid.Empty)
        {
            throw new ArgumentException("Identifier is required.", parameterName);
        }

        return identifier;
    }

    private static string NormalizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Project task title is required.", nameof(title));
        }

        return title.Trim();
    }

    private static string? NormalizeDescription(string? description)
        => string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    private static ProjectTaskPriority RequireDefinedPriority(ProjectTaskPriority priority)
    {
        if (!Enum.IsDefined(priority))
        {
            throw new ArgumentOutOfRangeException(nameof(priority), "Project task priority is invalid.");
        }

        return priority;
    }

    private void Touch() => UpdatedAt = DateTime.UtcNow;
}