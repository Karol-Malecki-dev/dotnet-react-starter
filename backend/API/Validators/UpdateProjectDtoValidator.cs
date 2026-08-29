using API.Contracts.Projects;
using FluentValidation;

namespace API.Validators;

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