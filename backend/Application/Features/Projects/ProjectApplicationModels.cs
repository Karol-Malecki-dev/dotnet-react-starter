using Domain.Enums;

namespace Application.Features.Projects;

public enum ProjectOperationStatus
{
    Success,
    NotFound,
    ValidationError,
    Conflict,
    Forbidden
}

public sealed record ProjectOperationResult<T>(
    ProjectOperationStatus Status,
    T? Value = default,
    string Message = "Success",
    int CreatedStatusCode = 200)
{
    public bool IsSuccess => Status == ProjectOperationStatus.Success;

    public static ProjectOperationResult<T> Success(T value, string message = "Success", int statusCode = 200)
        => new(ProjectOperationStatus.Success, value, message, statusCode);

    public static ProjectOperationResult<T> Failure(ProjectOperationStatus status, string message)
        => new(status, default, message);
}

public sealed record CreateProjectCommand(Guid OwnerId, string Name, string? Description);

public sealed record UpdateProjectCommand(Guid OwnerId, Guid ProjectId, string Name, string? Description);

public sealed record ProjectView(
    Guid Id,
    string Name,
    string? Description,
    Guid OwnerId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsArchived,
    ProjectMemberRole CurrentUserRole);

public sealed record ProjectMemberView(
    Guid UserId,
    string DisplayName,
    string Email,
    ProjectMemberRole Role,
    DateTime AddedAt);

public sealed record ProjectMemberUserView(Guid Id, string DisplayName, string Email);

public sealed record ProjectActivityView(Guid Id, string Type, string Description, Guid ActorUserId, string ActorDisplayName, Guid? ProjectTaskId, DateTime CreatedAt);
public sealed record PagedProjectActivityView(IReadOnlyList<ProjectActivityView> Items, int PageNumber, int PageSize, int TotalCount);

public sealed record ProjectDashboardView(
    int TotalTasks,
    int TodoTasks,
    int InProgressTasks,
    int DoneTasks,
    int LowPriorityTasks,
    int NormalPriorityTasks,
    int HighPriorityTasks,
    IReadOnlyList<ProjectTaskView> OverdueTasks,
    IReadOnlyList<ProjectTaskView> UpcomingTasks,
    IReadOnlyList<ProjectActivityView> RecentActivities);
