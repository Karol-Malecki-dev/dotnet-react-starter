using API.Modules.ProjectTasks.CreateProjectTask;
using Domain.Enums;
using FluentValidation.TestHelper;

namespace UnitTests.Modules.ProjectTasks.CreateProjectTask;

public sealed class CreateProjectTaskRequestValidatorTests
{
    private readonly CreateProjectTaskRequestValidator _validator = new();

    [Fact]
    public void Validate_returns_error_when_task_has_more_than_ten_labels()
    {
        var request = new CreateProjectTaskRequest(
            "Task title",
            null,
            ProjectTaskPriority.Normal,
            Labels: Enumerable.Range(1, 11).Select(index => $"label-{index}").ToArray());

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(task => task.Labels);
    }

    [Fact]
    public void Validate_accepts_a_valid_task_request()
    {
        var request = new CreateProjectTaskRequest(
            "Task title",
            "Description",
            ProjectTaskPriority.High,
            Labels: ["planning", "release"]);

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
