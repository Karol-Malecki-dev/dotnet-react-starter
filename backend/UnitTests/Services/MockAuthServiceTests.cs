using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace UnitTests.Services;

public class MockAuthServiceTests
{
    private readonly Mock<ILogger<MockAuthService>> _loggerMock = new();
    private readonly MockAuthService _service;

    public MockAuthServiceTests()
    {
        _service = new MockAuthService(_loggerMock.Object);
    }

    [Fact]
    public async Task AuthenticateAsync_ReturnsUser_When_credentials_are_valid()
    {
        var result = await _service.AuthenticateAsync("test@example.com", "password123");

        Assert.NotNull(result);
        Assert.Equal("test@example.com", result!.Email.Value);
        Assert.Equal(UserRole.User, result.Role);
    }

    [Fact]
    public async Task AuthenticateAsync_ReturnsNull_When_password_is_invalid()
    {
        var result = await _service.AuthenticateAsync("test@example.com", "wrong-password");

        Assert.Null(result);
    }

    [Fact]
    public async Task UserExistsAsync_Returns_true_for_existing_test_user_email()
    {
        var exists = await _service.UserExistsAsync("test@example.com");

        Assert.True(exists);
    }

    [Fact]
    public async Task UserExistsAsync_Returns_false_for_unknown_email()
    {
        var exists = await _service.UserExistsAsync("unknown@example.com");

        Assert.False(exists);
    }

    [Fact]
    public async Task RegisterAsync_Returns_user_with_correct_email_and_display_name()
    {
        var result = await _service.RegisterAsync("newuser@example.com", "password123", "New User");

        Assert.NotNull(result);
        Assert.Equal("newuser@example.com", result!.Email.Value);
        Assert.Equal("New User", result.DisplayName);
        Assert.NotEqual(Guid.Empty, result.Id);
    }
}
