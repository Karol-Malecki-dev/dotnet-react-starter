using Application.DTOs.Project;
using FluentValidation;

namespace API.Validators;

public class CreateProjectDtoValidator : AbstractValidator<CreateProjectDto>
{
    public CreateProjectDtoValidator()
    {
        RuleFor(project => project.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(project => project.Description)
            .MaximumLength(2000);
    }
}