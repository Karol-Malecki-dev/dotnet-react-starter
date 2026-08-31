using API.Contracts.Projects;
using API.Modules.Projects.UpdateProject;
using FluentValidation.TestHelper;

namespace UnitTests.Modules.Projects.UpdateProject;

public sealed class UpdateProjectRequestValidatorTests
{
    private readonly UpdateProjectRequestValidator _validator = new();

    [Fact]
    public void Validate_accepts_a_valid_project_request()
    {
        var result = _validator.TestValidate(
            new UpdateProjectRequest("Project name", "Project description", "current-stamp"));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_rejects_an_empty_project_name()
    {
        var result = _validator.TestValidate(
            new UpdateProjectRequest(" ", "Project description"));

        result.ShouldHaveValidationErrorFor(project => project.Name);
    }

    [Fact]
    public void Validate_rejects_fields_that_exceed_their_limits()
    {
        var result = _validator.TestValidate(
            new UpdateProjectRequest(
                new string('p', 201),
                new string('d', 2001),
                new string('s', 65)));

        result.ShouldHaveValidationErrorFor(project => project.Name);
        result.ShouldHaveValidationErrorFor(project => project.Description);
        result.ShouldHaveValidationErrorFor(project => project.ConcurrencyStamp);
    }
}
