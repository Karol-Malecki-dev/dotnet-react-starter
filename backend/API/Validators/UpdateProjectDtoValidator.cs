using Application.DTOs.Project;
using FluentValidation;

namespace API.Validators;

public class UpdateProjectDtoValidator : AbstractValidator<UpdateProjectDto>
{
    public UpdateProjectDtoValidator()
    {
        RuleFor(project => project.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(project => project.Description)
            .MaximumLength(2000);
    }
}