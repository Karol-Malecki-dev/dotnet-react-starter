using Domain.Entities;
using Domain.Enums;

namespace UnitTests.Domain;

public sealed class ProjectTaskTests
{
    [Fact]
    public void Create_normalizes_task_details_and_labels()
    {
        var projectId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();

        var task = ProjectTask.Create(
            projectId,
            "  Prepare release notes  ",
            "  Document the release.  ",
            ProjectTaskPriority.High,
            null,
            null,
            creatorId,
            [" Release ", "documentation", "release", " "]);

        Assert.Equal(projectId, task.ProjectId);
        Assert.Equal(creatorId, task.CreatedByUserId);
        Assert.Equal("Prepare release notes", task.Title);
        Assert.Equal("Document the release.", task.Description);
        Assert.Equal(ProjectTaskStatus.Todo, task.Status);
        Assert.Equal(["documentation", "release"], task.Labels.Select(label => label.Name));
    }

    [Fact]
    public void Task_domain_methods_change_only_their_respective_state()
    {
        var task = ProjectTask.Create(Guid.NewGuid(), "Initial title", null, ProjectTaskPriority.Normal, null, null, Guid.NewGuid());
        var assigneeId = Guid.NewGuid();
        var dueDate = DateTime.UtcNow.AddDays(3);

        task.Rename("Renamed task");
        task.ChangeDescription("Details");
        task.ChangePriority(ProjectTaskPriority.High);
        task.ChangeStatus(ProjectTaskStatus.InProgress);
        task.AssignTo(assigneeId);
        task.SetDueDate(dueDate);
        task.ReplaceLabels(["planning", "release"]);

        Assert.Equal("Renamed task", task.Title);
        Assert.Equal("Details", task.Description);
        Assert.Equal(ProjectTaskPriority.High, task.Priority);
        Assert.Equal(ProjectTaskStatus.InProgress, task.Status);
        Assert.Equal(assigneeId, task.AssignedUserId);
        Assert.Equal(dueDate, task.DueDate);
        Assert.Equal(["planning", "release"], task.Labels.Select(label => label.Name));

        task.Unassign();

        Assert.Null(task.AssignedUserId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_blank_title(string title)
    {
        Assert.Throws<ArgumentException>(() => ProjectTask.Create(
            Guid.NewGuid(), title, null, ProjectTaskPriority.Normal, null, null, Guid.NewGuid()));
    }

    [Fact]
    public void AssignTo_rejects_empty_user_identifier()
    {
        var task = ProjectTask.Create(Guid.NewGuid(), "Task", null, ProjectTaskPriority.Normal, null, null, Guid.NewGuid());

        Assert.Throws<ArgumentException>(() => task.AssignTo(Guid.Empty));
    }
}