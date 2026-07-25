using Application.DTOs.Project;
using FluentValidation;

namespace API.Validators;

public class CreateProjectTaskDtoValidator : AbstractValidator<CreateProjectTaskDto>
{
    public CreateProjectTaskDtoValidator()
    {
        RuleFor(task => task.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(task => task.Description)
            .MaximumLength(2000);

        RuleFor(task => task.Priority)
            .IsInEnum();
    }
}