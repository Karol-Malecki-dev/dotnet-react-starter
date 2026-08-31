using API.Controllers;
using Application.DTOs.Admin;
using Application.Interfaces;
using Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Shared.Responses;
using System.Security.Claims;

namespace UnitTests.Controllers;

public class AdminControllerTests
{
    private readonly Mock<IAdminService> _adminServiceMock = new();
    private readonly Mock<IAccountSecurityAuditWriter> _auditWriterMock = new();
    private readonly Mock<ILogger<AdminController>> _loggerMock = new();
    private readonly AdminController _controller;

    public AdminControllerTests()
    {
        _controller = new AdminController(
            _adminServiceMock.Object,
            _loggerMock.Object,
            _auditWriterMock.Object);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())],
                    "test"))
            }
        };

        _auditWriterMock
            .Setup(x => x.WriteAsync(It.IsAny<AccountSecurityAuditEntry>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task UpdateUserRole_writes_role_event_with_actor_and_subject()
    {
        var subjectUserId = Guid.NewGuid();
        var actorUserId = Guid.Parse(_controller.User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var response = ApiResponse<AdminUserDetailsDto>.Success(new AdminUserDetailsDto { Id = subjectUserId, Role = UserRole.Admin });
        _adminServiceMock
            .Setup(x => x.UpdateUserRoleAsync(subjectUserId, UserRole.Admin, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        await _controller.UpdateUserRoleAsync(subjectUserId, UserRole.Admin, CancellationToken.None);

        _auditWriterMock.Verify(x => x.WriteAsync(
            It.Is<AccountSecurityAuditEntry>(entry =>
                entry.EventCode == "account.role.changed" &&
                entry.Outcome == "success" &&
                entry.ActorUserId == actorUserId &&
                entry.SubjectUserId == subjectUserId &&
                entry.Metadata!["reason"] == "Admin"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeactivateUser_writes_status_event_with_subject()
    {
        var subjectUserId = Guid.NewGuid();
        var response = ApiResponse<AdminUserDetailsDto>.Success(new AdminUserDetailsDto { Id = subjectUserId, IsActive = false });
        _adminServiceMock
            .Setup(x => x.DeactivateUserAsync(subjectUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        await _controller.DeactivateUserAsync(subjectUserId, CancellationToken.None);

        _auditWriterMock.Verify(x => x.WriteAsync(
            It.Is<AccountSecurityAuditEntry>(entry =>
                entry.EventCode == "account.status.changed" &&
                entry.SubjectUserId == subjectUserId &&
                entry.Metadata!["reason"] == "inactive"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ActivateUser_does_not_write_event_when_user_is_missing()
    {
        var subjectUserId = Guid.NewGuid();
        _adminServiceMock
            .Setup(x => x.ActivateUserAsync(subjectUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<AdminUserDetailsDto>.Error(404, "User not found"));

        var result = await _controller.ActivateUserAsync(subjectUserId, CancellationToken.None);

        Assert.Equal(404, Assert.IsType<ObjectResult>(result.Result).StatusCode);
        _auditWriterMock.Verify(x => x.WriteAsync(
            It.IsAny<AccountSecurityAuditEntry>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
