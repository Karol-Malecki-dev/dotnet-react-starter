using Application.Features.ProjectManagement.Tasks;
using Application.Features.Projects;
using Application.Modules.ProjectTasks.GetProjectTaskDetails;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Modules.ProjectTasks.GetProjectTaskDetails;
using Moq;

namespace UnitTests.Modules.ProjectTasks.GetProjectTaskDetails;

public sealed class GetProjectTaskDetailsHandlerTests
{
    private readonly Mock<IProjectTaskAccess> _access = new();

    [Fact]
    public async Task Handle_returns_not_found_without_loading_task_when_user_has_no_active_project_role()
    {
        var query = CreateQuery();
        _access
            .Setup(access => access.GetActiveProjectRoleAsync(
                query.UserId,
                query.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProjectMemberRole?)null);

        var result = await CreateHandler().HandleAsync(query);

        Assert.Equal(ProjectOperationStatus.NotFound, result.Status);
        Assert.Equal("Project task not found", result.Message);
        _access.Verify(
            access => access.GetTaskWithLabelsAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_returns_not_found_when_task_does_not_exist()
    {
        var query = CreateQuery();
        _access
            .Setup(access => access.GetActiveProjectRoleAsync(
                query.UserId,
                query.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProjectMemberRole.Member);
        _access
            .Setup(access => access.GetTaskWithLabelsAsync(
                query.ProjectId,
                query.TaskId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProjectTask?)null);

        var result = await CreateHandler().HandleAsync(query);

        Assert.Equal(ProjectOperationStatus.NotFound, result.Status);
        Assert.Equal("Project task not found", result.Message);
    }

    [Fact]
    public async Task Handle_returns_complete_task_view_for_an_authorized_member()
    {
        var task = ProjectTask.Create(
            Guid.NewGuid(),
            "Task title",
            "Task description",
            ProjectTaskPriority.High,
            DateTime.UtcNow.AddDays(2),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ["zeta", "alpha"]);
        var query = new GetProjectTaskDetailsQuery(Guid.NewGuid(), task.ProjectId, task.Id);
        var cancellationToken = new CancellationTokenSource().Token;

        _access
            .Setup(access => access.GetActiveProjectRoleAsync(
                query.UserId,
                query.ProjectId,
                cancellationToken))
            .ReturnsAsync(ProjectMemberRole.Member);
        _access
            .Setup(access => access.GetTaskWithLabelsAsync(
                query.ProjectId,
                query.TaskId,
                cancellationToken))
            .ReturnsAsync(task);

        var result = await CreateHandler().HandleAsync(query, cancellationToken);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(task.Id, result.Value.Id);
        Assert.Equal(task.ProjectId, result.Value.ProjectId);
        Assert.Equal(task.Title, result.Value.Title);
        Assert.Equal(task.Description, result.Value.Description);
        Assert.Equal(task.Status, result.Value.Status);
        Assert.Equal(task.Priority, result.Value.Priority);
        Assert.Equal(task.DueDate, result.Value.DueDate);
        Assert.Equal(task.AssignedUserId, result.Value.AssignedUserId);
        Assert.Equal(task.CreatedByUserId, result.Value.CreatedByUserId);
        Assert.Equal(task.CreatedAt, result.Value.CreatedAt);
        Assert.Equal(task.UpdatedAt, result.Value.UpdatedAt);
        Assert.Equal(task.ConcurrencyStamp, result.Value.ConcurrencyStamp);
        Assert.Equal(["alpha", "zeta"], result.Value.Labels);
        _access.Verify(access => access.GetActiveProjectRoleAsync(
            query.UserId,
            query.ProjectId,
            cancellationToken), Times.Once);
        _access.Verify(access => access.GetTaskWithLabelsAsync(
            query.ProjectId,
            query.TaskId,
            cancellationToken), Times.Once);
    }

    private GetProjectTaskDetailsHandler CreateHandler()
        => new(_access.Object);

    private static GetProjectTaskDetailsQuery CreateQuery()
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
}
