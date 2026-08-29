using Domain.Enums;

namespace API.Contracts.Projects;

public sealed record CreateProjectRequest(string Name, string? Description);

public sealed record UpdateProjectRequest(string Name, string? Description, string? ConcurrencyStamp = null);

public sealed record AddProjectMemberRequest(Guid UserId);

public sealed record UpdateProjectMemberRoleRequest(ProjectMemberRole Role);
