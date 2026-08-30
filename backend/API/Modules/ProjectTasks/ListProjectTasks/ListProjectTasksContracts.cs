using Application.Modules.ProjectTasks.ListProjectTasks;
using Domain.Enums;

namespace API.Modules.ProjectTasks.ListProjectTasks;

/// <summary>
/// HTTP query parameters for listing project tasks.
/// </summary>
public sealed record ProjectTaskQueryRequest(
    int PageNumber = 1,
    int PageSize = 20,
    string? Search = null,
    ProjectTaskStatus? Status = null,
    ProjectTaskPriority? Priority = null,
    Guid? AssignedUserId = null,
    string? Label = null,
    DateTime? DueBefore = null,
    ProjectTaskSortBy SortBy = ProjectTaskSortBy.DueDate,
    SortDirection SortDirection = SortDirection.Ascending);
