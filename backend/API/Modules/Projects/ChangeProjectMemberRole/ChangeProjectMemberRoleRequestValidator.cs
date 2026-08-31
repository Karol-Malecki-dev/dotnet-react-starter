using API.Contracts.Projects;
using FluentValidation;

namespace API.Modules.Projects.ChangeProjectMemberRole;

/// <summary>
/// Validates project-member role changes before the handler is invoked.
/// </summary>
public sealed class ChangeProjectMemberRoleRequestValidator : AbstractValidator<UpdateProjectMemberRoleRequest>
{
    public ChangeProjectMemberRoleRequestValidator()
    {
        RuleFor(request => request.Role)
            .IsInEnum();
    }
}
