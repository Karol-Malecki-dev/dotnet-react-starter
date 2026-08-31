using API.Contracts.Projects;
using API.Modules.Projects.AddProjectMember;
using API.Modules.Projects.ChangeProjectMemberRole;
using Domain.Enums;
using FluentValidation.TestHelper;

namespace UnitTests.Modules.Projects;

public sealed class ProjectMembershipRequestValidatorTests
{
    [Fact]
    public void Add_member_validator_rejects_empty_user_identifier()
    {
        var result = new AddProjectMemberRequestValidator()
            .TestValidate(new AddProjectMemberRequest(Guid.Empty));

        result.ShouldHaveValidationErrorFor(request => request.UserId);
    }

    [Theory]
    [InlineData(ProjectMemberRole.Member)]
    [InlineData(ProjectMemberRole.Viewer)]
    [InlineData(ProjectMemberRole.Owner)]
    public void Change_role_validator_accepts_defined_roles(ProjectMemberRole role)
    {
        var result = new ChangeProjectMemberRoleRequestValidator()
            .TestValidate(new UpdateProjectMemberRoleRequest(role));

        result.ShouldNotHaveValidationErrorFor(request => request.Role);
    }

    [Fact]
    public void Change_role_validator_rejects_unknown_role()
    {
        var result = new ChangeProjectMemberRoleRequestValidator()
            .TestValidate(new UpdateProjectMemberRoleRequest((ProjectMemberRole)999));

        result.ShouldHaveValidationErrorFor(request => request.Role);
    }
}
