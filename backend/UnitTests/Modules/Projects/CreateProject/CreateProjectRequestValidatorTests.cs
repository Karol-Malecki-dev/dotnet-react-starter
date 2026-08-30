using API.Contracts.Projects;
using API.Modules.Projects.CreateProject;
using FluentValidation.TestHelper;

namespace UnitTests.Modules.Projects.CreateProject;

public sealed class CreateProjectRequestValidatorTests
{
    private readonly CreateProjectRequestValidator _validator = new();

    [Fact]
    public void Validate_accepts_a_valid_project_request()
    {
        var result = _validator.TestValidate(
            new CreateProjectRequest("Project name", "Project description"));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_rejects_an_empty_project_name()
    {
        var result = _validator.TestValidate(
            new CreateProjectRequest(" ", "Project description"));

        result.ShouldHaveValidationErrorFor(project => project.Name);
    }

    [Fact]
    public void Validate_rejects_project_fields_that_exceed_their_limits()
    {
        var result = _validator.TestValidate(
            new CreateProjectRequest(
                new string('p', 201),
                new string('d', 2001)));

        result.ShouldHaveValidationErrorFor(project => project.Name);
        result.ShouldHaveValidationErrorFor(project => project.Description);
    }
}
