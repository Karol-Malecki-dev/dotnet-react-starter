using Application.Features.ProjectManagement.Tasks;
using MediatR;
using Application.Features.Projects;
using Domain.Enums;

namespace Application.Modules.ProjectTasks.ListProjectTasks;

/// <summary>
/// Represents the application input for listing tasks in an accessible project.
/// </summary>
public sealed record ProjectTaskQuery(
    Guid UserId,
    Guid ProjectId,
    int PageNumber,
    int PageSize,
    string? Search,
    ProjectTaskStatus? Status,
    ProjectTaskPriority? Priority,
    Guid? AssignedUserId,
    string? Label,
    DateTime? DueBefore,
    ProjectTaskSortBy SortBy,
    SortDirection SortDirection)
    : IRequest<ProjectOperationResult<PagedProjectTaskView>>;

/// <summary>
/// Defines the supported task list sort fields.
/// </summary>
public enum ProjectTaskSortBy
{
    DueDate,
    CreatedAt,
    Priority
}

/// <summary>
/// Defines the direction used when sorting the task list.
/// </summary>
public enum SortDirection
{
    Ascending,
    Descending
}

/// <summary>
/// Represents a paged result returned by the list-project-tasks use case.
/// </summary>
public sealed record PagedProjectTaskView(
    IReadOnlyList<ProjectTaskView> Items,
    int PageNumber,
    int PageSize,
    int TotalCount);

