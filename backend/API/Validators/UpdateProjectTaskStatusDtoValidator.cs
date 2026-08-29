using API.Contracts.Projects;
using Domain.Enums;
using FluentValidation;

namespace API.Validators;

public sealed class UpdateProjectTaskStatusRequestValidator : AbstractValidator<UpdateProjectTaskStatusRequest>
{
    public UpdateProjectTaskStatusRequestValidator()
    {
        RuleFor(task => task.Status)
            .IsInEnum()
            .Must(status => status is ProjectTaskStatus.Todo
                or ProjectTaskStatus.InProgress
                or ProjectTaskStatus.Done);

        RuleFor(task => task.ConcurrencyStamp)
            .NotEmpty()
            .MaximumLength(64);
    }
}