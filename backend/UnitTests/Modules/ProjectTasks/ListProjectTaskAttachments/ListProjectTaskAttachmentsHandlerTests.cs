using Application.Features.ProjectManagement.Tasks;
using Application.Features.Projects;
using Application.Modules.ProjectTasks.Attachments;
using Application.Modules.ProjectTasks.ListProjectTaskAttachments;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Modules.ProjectTasks.ListProjectTaskAttachments;
using Moq;

namespace UnitTests.Modules.ProjectTasks.ListProjectTaskAttachments;

public sealed class ListProjectTaskAttachmentsHandlerTests
{
    private readonly Mock<IProjectTaskAccess> _access = new();
    private readonly Mock<IListProjectTaskAttachmentsQueryStore> _queryStore = new();

    [Fact]
    public async Task Handle_returns_not_found_without_querying_when_user_has_no_access()
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
        _queryStore.Verify(
            store => store.QueryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_returns_attachment_metadata_for_an_accessible_task()
    {
        var query = CreateQuery();
        ConfigureAccess(query, ProjectTask.Create(
            query.ProjectId,
            "Task",
            null,
            ProjectTaskPriority.Normal,
            null,
            null,
            query.UserId));
        var expected = new List<ProjectTaskAttachmentView>
        {
            new(Guid.NewGuid(), query.TaskId, query.UserId, "Owner", "notes.txt", "text/plain", 10, DateTime.UtcNow)
        };
        _queryStore
            .Setup(store => store.QueryAsync(query.TaskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await CreateHandler().HandleAsync(query);

        Assert.True(result.IsSuccess);
        Assert.Same(expected, result.Value);
    }

    private ListProjectTaskAttachmentsHandler CreateHandler()
        => new(_access.Object, _queryStore.Object);

    private void ConfigureAccess(
        ListProjectTaskAttachmentsQuery query,
        ProjectTask task)
    {
        _access
            .Setup(access => access.GetActiveProjectRoleAsync(
                query.UserId,
                query.ProjectId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProjectMemberRole.Owner);
        _access
            .Setup(access => access.GetTaskWithLabelsAsync(
                query.ProjectId,
                query.TaskId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);
    }

    private static ListProjectTaskAttachmentsQuery CreateQuery()
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
}
