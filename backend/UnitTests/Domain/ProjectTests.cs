using Domain.Entities;
using Domain.Enums;

namespace UnitTests.Domain;

public sealed class ProjectTests
{
    [Fact]
    public void Create_normalizes_project_details()
    {
        var ownerId = Guid.NewGuid();

        var project = Project.Create(ownerId, "  Release planning  ", "  Prepare the next release.  ");

        Assert.Equal(ownerId, project.OwnerId);
        Assert.Equal("Release planning", project.Name);
        Assert.Equal("Prepare the next release.", project.Description);
        Assert.False(project.IsArchived);
        var ownerMembership = Assert.Single(project.Members);
        Assert.Equal(ownerId, ownerMembership.UserId);
        Assert.Equal(ProjectMemberRole.Owner, ownerMembership.Role);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_blank_name(string name)
    {
        Assert.Throws<ArgumentException>(() => Project.Create(Guid.NewGuid(), name));
    }

    [Fact]
    public void Create_rejects_empty_owner_identifier()
    {
        Assert.Throws<ArgumentException>(() => Project.Create(Guid.Empty, "Project"));
    }

    [Fact]
    public void Domain_methods_update_project_state()
    {
        var project = Project.Create(Guid.NewGuid(), "Initial name", "Initial description");

        project.Rename("  Updated name  ");
        project.ChangeDescription("  Updated description  ");
        project.Archive();

        Assert.Equal("Updated name", project.Name);
        Assert.Equal("Updated description", project.Description);
        Assert.True(project.IsArchived);
    }

    [Fact]
    public void Membership_methods_enforce_project_invariants()
    {
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var project = Project.Create(ownerId, "Project");

        var member = project.AddMember(memberId, ProjectMemberRole.Viewer);
        Assert.Equal(ProjectMemberRole.Viewer, member.Role);

        project.ChangeMemberRole(memberId, ProjectMemberRole.Member);
        Assert.Equal(ProjectMemberRole.Member, member.Role);

        project.RemoveMember(memberId);

        Assert.Single(project.Members);
        Assert.Equal(ownerId, project.Members.Single().UserId);
        Assert.Throws<InvalidOperationException>(() => project.RemoveMember(ownerId));
    }
}