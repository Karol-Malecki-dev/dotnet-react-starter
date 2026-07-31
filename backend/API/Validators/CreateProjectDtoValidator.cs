using API.Contracts.Projects;
using FluentValidation;

namespace API.Validators;

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