using Domain.Enums;

namespace Domain.Entities;

public class Project
{
    private Project()
    {
    }

    private Project(Guid ownerId, string name, string? description)
    {
        Id = Guid.NewGuid();
        OwnerId = RequireIdentifier(ownerId, nameof(ownerId));
        Name = NormalizeName(name);
        Description = NormalizeDescription(description);
        ConcurrencyStamp = GenerateConcurrencyStamp();
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
        _members.Add(ProjectMember.CreateOwner(Id, OwnerId));
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid OwnerId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public bool IsArchived { get; private set; }
    public string ConcurrencyStamp { get; private set; } = string.Empty;

    private readonly List<ProjectMember> _members = [];

    public IReadOnlyCollection<ProjectMember> Members => _members;
    public ICollection<ProjectInvitation> Invitations { get; set; } = [];

    public static Project Create(Guid ownerId, string name, string? description = null)
        => new(ownerId, name, description);

    public ProjectMember AddMember(Guid userId, ProjectMemberRole role = ProjectMemberRole.Member)
    {
        RequireIdentifier(userId, nameof(userId));
        if (userId == OwnerId)
        {
            throw new InvalidOperationException("The project owner is already a member.");
        }

        if (_members.Any(member => member.UserId == userId))
        {
            throw new InvalidOperationException("User is already a project member.");
        }

        var member = ProjectMember.Create(Id, userId, role);
        _members.Add(member);
        Touch();
        return member;
    }

    public ProjectMember ChangeMemberRole(Guid userId, ProjectMemberRole role)
    {
        RequireIdentifier(userId, nameof(userId));
        if (userId == OwnerId)
        {
            throw new InvalidOperationException("The project owner role cannot be changed.");
        }

        var member = FindMember(userId);
        member.ChangeRole(role);
        Touch();
        return member;
    }

    public ProjectMember RemoveMember(Guid userId)
    {
        RequireIdentifier(userId, nameof(userId));
        if (userId == OwnerId)
        {
            throw new InvalidOperationException("The project owner cannot be removed.");
        }

        var member = FindMember(userId);
        _members.Remove(member);
        Touch();
        return member;
    }

    public void Rename(string name)
    {
        Name = NormalizeName(name);
        Touch();
    }

    public void ChangeDescription(string? description)
    {
        Description = NormalizeDescription(description);
        Touch();
    }

    public void Archive()
    {
        if (IsArchived)
        {
            return;
        }

        IsArchived = true;
        Touch();
    }

    private static Guid RequireIdentifier(Guid identifier, string parameterName)
    {
        if (identifier == Guid.Empty)
        {
            throw new ArgumentException("Identifier is required.", parameterName);
        }

        return identifier;
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Project name is required.", nameof(name));
        }

        return name.Trim();
    }

    private static string? NormalizeDescription(string? description)
        => string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    private ProjectMember FindMember(Guid userId)
        => _members.FirstOrDefault(member => member.UserId == userId)
            ?? throw new InvalidOperationException("Project member was not found.");

    private void Touch()
    {
        UpdatedAt = DateTime.UtcNow;
        ConcurrencyStamp = GenerateConcurrencyStamp();
    }

    private static string GenerateConcurrencyStamp()
        => Guid.NewGuid().ToString("N");
}