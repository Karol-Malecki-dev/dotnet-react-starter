using Application.DTOs.Auth;
using FluentValidation;
using FluentValidation.TestHelper;
using Xunit;

namespace UnitTests.Validators;

public class ForgotPasswordRequestDtoValidatorTests
{
    private readonly IValidator<ForgotPasswordRequestDto> _validator = null!;

    public ForgotPasswordRequestDtoValidatorTests()
    {
        // Intentionally left blank until a dedicated validator exists.
        // These tests will be added once the validator is introduced.
    }
}