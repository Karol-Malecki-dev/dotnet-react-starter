using API.Contracts.Projects;
using FluentValidation;

namespace API.Modules.Projects.UpdateProject;

/// <summary>
/// Validates the HTTP input for the update-project slice.
/// </summary>
public sealed class UpdateProjectRequestValidator : AbstractValidator<UpdateProjectRequest>
{
    public UpdateProjectRequestValidator()
    {
        RuleFor(project => project.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(project => project.Description)
            .MaximumLength(2000);

        RuleFor(project => project.ConcurrencyStamp)
            .MaximumLength(64);
    }
}
