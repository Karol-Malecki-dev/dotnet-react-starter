using Application.DTOs.Project;
using Domain.Enums;
using FluentValidation;

namespace API.Validators;

public class UpdateProjectTaskStatusDtoValidator : AbstractValidator<UpdateProjectTaskStatusDto>
{
    public UpdateProjectTaskStatusDtoValidator()
    {
        RuleFor(task => task.Status)
            .IsInEnum()
            .Must(status => status is ProjectTaskStatus.Todo
                or ProjectTaskStatus.InProgress
                or ProjectTaskStatus.Done);
    }
}