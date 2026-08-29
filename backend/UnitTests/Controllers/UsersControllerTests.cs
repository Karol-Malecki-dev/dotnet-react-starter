using API.Controllers;
using Application.DTOs.User;
using Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Responses;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnitTests.TestHelpers;
using Xunit;

namespace UnitTests.Controllers;

public class UsersControllerTests
{
    private readonly Mock<IUserService> _userServiceMock = new();
    private readonly Mock<ILogger<UsersController>> _loggerMock = new();
    private readonly UsersController _controller;

    public UsersControllerTests()
    {
        _controller = new UsersController(_userServiceMock.Object, _loggerMock.Object);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = ControllerTestHelper.CreateHttpContext(ControllerTestHelper.CreateAuthenticatedUser(Guid.NewGuid().ToString(), "user@test.com"))
        };
    }

    [Fact]
    public async Task GetUserById_Returns_ok_with_user_data()
    {
        var userId = Guid.NewGuid();
        var userDto = new UserDto { Id = userId, Email = "user@test.com", FirstName = "John", LastName = "Doe" };

        _userServiceMock.Setup(x => x.GetUserByIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(ApiResponse<UserDto>.Success(userDto));

        var actionResult = await _controller.GetUserById(userId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<ApiResponse<UserDto>>(okResult.Value);

        Assert.Equal(200, okResult.StatusCode);
        Assert.Equal(userDto.Email, response.Data?.Email);
    }

    [Fact]
    public async Task GetAllUsers_Returns_paged_user_list()
    {
        var users = new List<UserDto>
        {
            new() { Id = Guid.NewGuid(), Email = "user1@test.com", FirstName = "User", LastName = "One" },
            new() { Id = Guid.NewGuid(), Email = "user2@test.com", FirstName = "User", LastName = "Two" }
        };

        _userServiceMock.Setup(x => x.GetAllUsersPagedAsync(1, 10, It.IsAny<CancellationToken>())).ReturnsAsync(ApiResponse<List<UserDto>>.Success(users));

        var actionResult = await _controller.GetAllUsers();

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<ApiResponse<List<UserDto>>>(okResult.Value);

        Assert.Equal(2, response.Data?.Count);
    }

    [Fact]
    public async Task GetCount_Returns_ok_with_total_user_count()
    {
        _userServiceMock.Setup(x => x.GetUserCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(ApiResponse<int>.Success(42));

        var actionResult = await _controller.GetCount(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<ApiResponse<int>>(okResult.Value);

        Assert.Equal(42, response.Data);
    }

    [Fact]
    public async Task Delete_Returns_ok_when_user_is_deleted()
    {
        var userId = Guid.NewGuid();
        _userServiceMock.Setup(x => x.DeleteUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(ApiResponse<bool>.Success(true));

        var actionResult = await _controller.Delete(userId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<ApiResponse<bool>>(okResult.Value);

        Assert.True(response.Data);
    }

    [Fact]
    public async Task UpdateDisplayName_Returns_ok_when_updated()
    {
        var userId = Guid.NewGuid();
        var expected = new UserDto { Id = userId, Email = "user@test.com", FirstName = "New", LastName = "Name" };

        _userServiceMock.Setup(x => x.UpdateDisplayNameAsync(userId, "NewName", It.IsAny<CancellationToken>())).ReturnsAsync(ApiResponse<UserDto>.Success(expected));

        var actionResult = await _controller.UpdateDisplayName(userId, "NewName", CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<ApiResponse<UserDto>>(okResult.Value);

        Assert.Equal(expected.Id, response.Data?.Id);
        Assert.Equal("New", response.Data?.FirstName);
    }

    [Fact]
    public async Task UpdateRole_Returns_ok_when_role_updated()
    {
        var userId = Guid.NewGuid();
        var expected = new UserDto { Id = userId, Email = "user@test.com", FirstName = "John", LastName = "Doe" };

        _userServiceMock.Setup(x => x.UpdateUserRoleAsync(userId, "Admin", It.IsAny<CancellationToken>())).ReturnsAsync(ApiResponse<UserDto>.Success(expected));

        var actionResult = await _controller.UpdateRole(userId, "Admin", CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var response = Assert.IsType<ApiResponse<UserDto>>(okResult.Value);

        Assert.Equal(expected.Id, response.Data?.Id);
        Assert.Equal(expected.Email, response.Data?.Email);
    }
}
