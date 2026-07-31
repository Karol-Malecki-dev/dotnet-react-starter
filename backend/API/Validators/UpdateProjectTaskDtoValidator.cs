using API.Contracts.Projects;
using FluentValidation;

namespace API.Validators;

public sealed class UpdateProjectTaskRequestValidator : AbstractValidator<UpdateProjectTaskRequest>
{
    public UpdateProjectTaskRequestValidator()
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