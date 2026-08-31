using API.Contracts.Projects;
using FluentValidation;

namespace API.Modules.Projects.AddProjectMember;

/// <summary>
/// Validates the user identifier supplied to the add-project-member slice.
/// </summary>
public sealed class AddProjectMemberRequestValidator : AbstractValidator<AddProjectMemberRequest>
{
    public AddProjectMemberRequestValidator()
    {
        RuleFor(request => request.UserId)
            .NotEmpty();
    }
}
