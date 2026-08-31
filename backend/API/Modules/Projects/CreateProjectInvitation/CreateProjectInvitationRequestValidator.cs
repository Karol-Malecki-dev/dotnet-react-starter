using API.Contracts.Projects;
using Domain.Enums;
using FluentValidation;

namespace API.Modules.Projects.CreateProjectInvitation;

/// <summary>
/// Validates the transport contract for project invitation creation.
/// </summary>
public sealed class CreateProjectInvitationRequestValidator : AbstractValidator<CreateProjectInvitationRequest>
{
    public CreateProjectInvitationRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Email must be a valid email address")
            .MaximumLength(256).WithMessage("Email must be at most 256 characters");

        RuleFor(request => request.Role)
            .IsInEnum().WithMessage("Invitation role is invalid")
            .Must(role => role is ProjectMemberRole.Member or ProjectMemberRole.Viewer)
            .WithMessage("Invitation role must be Member or Viewer");
    }
}
