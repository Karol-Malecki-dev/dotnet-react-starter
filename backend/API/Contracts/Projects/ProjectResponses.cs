using Domain.Enums;

namespace API.Contracts.Projects;

public sealed record ProjectResponse(
    Guid Id,
    string Name,
    string? Description,
    Guid OwnerId,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string ConcurrencyStamp,
    bool IsArchived,
    ProjectMemberRole CurrentUserRole);

public sealed record ProjectMemberResponse(
    Guid UserId,
    string DisplayName,
    string Email,
    ProjectMemberRole Role,
    DateTime AddedAt);

public sealed record ProjectMemberUserResponse(Guid Id, string DisplayName, string Email);
