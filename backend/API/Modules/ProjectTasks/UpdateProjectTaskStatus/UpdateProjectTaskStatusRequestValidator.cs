using Domain.Enums;
using FluentValidation;

namespace API.Modules.ProjectTasks.UpdateProjectTaskStatus;

/// <summary>
/// Validates status changes before the use case handler is invoked.
/// </summary>
public sealed class UpdateProjectTaskStatusRequestValidator : AbstractValidator<UpdateProjectTaskStatusRequest>
{
    public UpdateProjectTaskStatusRequestValidator()
    {
        RuleFor(request => request.Status)
            .IsInEnum()
            .Must(status => status is ProjectTaskStatus.Todo
                or ProjectTaskStatus.InProgress
                or ProjectTaskStatus.Done);

        RuleFor(request => request.ConcurrencyStamp)
            .NotEmpty()
            .MaximumLength(64);
    }
}
