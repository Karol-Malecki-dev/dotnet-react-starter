using FluentValidation;

namespace API.Modules.ProjectTasks.CreateProjectTask;

/// <summary>
/// Validates the HTTP input for the create-project-task slice.
/// </summary>
public sealed class CreateProjectTaskRequestValidator : AbstractValidator<CreateProjectTaskRequest>
{
    public CreateProjectTaskRequestValidator()
    {
        RuleFor(task => task.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(task => task.Description)
            .MaximumLength(2000);

        RuleFor(task => task.Priority)
            .IsInEnum();

        RuleForEach(task => task.Labels)
            .NotEmpty()
            .MaximumLength(40);

        RuleFor(task => task.Labels)
            .Must(labels => labels is null || labels.Count <= 10)
            .WithMessage("A task cannot have more than 10 labels");
    }
}
