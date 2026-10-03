using Domain.Enums;

namespace Application.Modules.Projects.Shared;

public sealed record ProjectView(
    Guid Id,
    string Name,
    string? Description,
    Guid OwnerId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string ConcurrencyStamp,
    bool IsArchived,
    ProjectMemberRole CurrentUserRole);

public sealed record ProjectMemberView(
    Guid UserId,
    string DisplayName,
    string Email,
    ProjectMemberRole Role,
    DateTime AddedAt);

public sealed record ProjectMemberUserView(Guid Id, string DisplayName, string Email);
