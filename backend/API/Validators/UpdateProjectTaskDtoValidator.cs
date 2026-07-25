using Application.DTOs.Project;
using FluentValidation;

namespace API.Validators;

public class UpdateProjectTaskDtoValidator : AbstractValidator<UpdateProjectTaskDto>
{
    public UpdateProjectTaskDtoValidator()
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