namespace Application.Modules.Projects.Shared;

public sealed record ProjectActivityView(
    Guid Id,
    string Type,
    string Description,
    Guid ActorUserId,
    string ActorDisplayName,
    Guid? ProjectTaskId,
    DateTime CreatedAt);

public sealed record PagedProjectActivityView(
    IReadOnlyList<ProjectActivityView> Items,
    int PageNumber,
    int PageSize,
    int TotalCount);
