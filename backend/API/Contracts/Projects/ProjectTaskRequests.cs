using Application.Features.ProjectManagement.Tasks;
using Domain.Enums;

namespace API.Contracts.Projects;

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

public sealed record CreateProjectTaskRequest(
    string Title,
    string? Description,
    ProjectTaskPriority Priority = ProjectTaskPriority.Normal,
    DateTime? DueDate = null,
    Guid? AssignedUserId = null,
    IReadOnlyList<string>? Labels = null);

public sealed record UpdateProjectTaskRequest(
    string Title,
    string? Description,
    ProjectTaskPriority Priority = ProjectTaskPriority.Normal,
    DateTime? DueDate = null,
    Guid? AssignedUserId = null,
    IReadOnlyList<string>? Labels = null,
    string? ConcurrencyStamp = null);

public sealed record UpdateProjectTaskStatusRequest(ProjectTaskStatus Status, string? ConcurrencyStamp = null);
