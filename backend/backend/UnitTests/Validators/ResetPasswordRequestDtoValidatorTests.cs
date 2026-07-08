using Application.DTOs.Auth;
using FluentValidation;
using FluentValidation.TestHelper;
using Xunit;

namespace UnitTests.Validators;

public class ResetPasswordRequestDtoValidatorTests
{
    private readonly IValidator<ResetPasswordRequestDto> _validator = null!;

    public ResetPasswordRequestDtoValidatorTests()
    {
        // Intentionally left blank until a dedicated validator exists.
        // These tests will be added once the validator is introduced.
    }
}