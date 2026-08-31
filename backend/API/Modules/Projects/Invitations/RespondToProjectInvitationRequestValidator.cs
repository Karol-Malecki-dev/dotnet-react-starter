using API.Contracts.Projects;
using FluentValidation;

namespace API.Modules.Projects.Invitations;

/// <summary>
/// Validates the shared invitation response request.
/// </summary>
public sealed class RespondToProjectInvitationRequestValidator : AbstractValidator<RespondToProjectInvitationRequest>
{
    public RespondToProjectInvitationRequestValidator()
    {
        RuleFor(request => request.Token)
            .NotEmpty().WithMessage("Invitation token is required")
            .MaximumLength(128).WithMessage("Invitation token must be at most 128 characters");
    }
}
