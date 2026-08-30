using API.Modules.ProjectTasks.UpdateProjectTaskStatus;
using Domain.Enums;
using FluentValidation.TestHelper;

namespace UnitTests.Modules.ProjectTasks.UpdateProjectTaskStatus;

public sealed class UpdateProjectTaskStatusRequestValidatorTests
{
    private readonly UpdateProjectTaskStatusRequestValidator _validator = new();

    [Fact]
    public void Accepts_supported_status_with_concurrency_stamp()
    {
        var result = _validator.TestValidate(new UpdateProjectTaskStatusRequest(
            ProjectTaskStatus.InProgress,
            "current-stamp"));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Rejects_missing_concurrency_stamp()
    {
        var result = _validator.TestValidate(new UpdateProjectTaskStatusRequest(
            ProjectTaskStatus.InProgress));

        result.ShouldHaveValidationErrorFor(request => request.ConcurrencyStamp);
    }

    [Fact]
    public void Rejects_unsupported_status()
    {
        var result = _validator.TestValidate(new UpdateProjectTaskStatusRequest(
            (ProjectTaskStatus)999,
            "current-stamp"));

        result.ShouldHaveValidationErrorFor(request => request.Status);
    }

    [Fact]
    public void Rejects_a_concurrency_stamp_longer_than_64_characters()
    {
        var result = _validator.TestValidate(new UpdateProjectTaskStatusRequest(
            ProjectTaskStatus.Done,
            new string('x', 65)));

        result.ShouldHaveValidationErrorFor(request => request.ConcurrencyStamp);
    }
}
