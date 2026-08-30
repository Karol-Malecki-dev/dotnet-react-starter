using API.Contracts.Projects;
using FluentValidation;

namespace API.Modules.Projects.CreateProject;

/// <summary>
/// Validates the HTTP input for the create-project slice.
/// </summary>
public sealed class CreateProjectRequestValidator : AbstractValidator<CreateProjectRequest>
{
    public CreateProjectRequestValidator()
    {
        RuleFor(project => project.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(project => project.Description)
            .MaximumLength(2000);
    }
}
