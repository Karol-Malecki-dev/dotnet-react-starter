using API.Controllers;
using Application.DTOs.Notification;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Shared.Responses;
using System.Threading;
using System.Threading.Tasks;
using UnitTests.TestHelpers;
using Xunit;

namespace UnitTests.Controllers;

public class NotificationsControllerTests
{
    [Fact]
    public async Task GetUnreadCount_Forwards_request_cancellation_token()
    {
        var userId = Guid.NewGuid();
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;
        var notificationServiceMock = new Mock<INotificationService>();
        notificationServiceMock
            .Setup(service => service.GetUnreadCountAsync(userId, It.Is<CancellationToken>(token => token == cancellationToken)))
            .ReturnsAsync(ApiResponse<int>.Success(3));

        var controller = new NotificationsController(notificationServiceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = ControllerTestHelper.CreateHttpContext(
                    ControllerTestHelper.CreateAuthenticatedUser(userId.ToString(), "user@test.com"))
            }
        };

        var actionResult = await controller.GetUnreadCount(cancellationToken);

        var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
        var response = Assert.IsType<ApiResponse<int>>(objectResult.Value);
        Assert.Equal(3, response.Data);
        notificationServiceMock.Verify(
            service => service.GetUnreadCountAsync(userId, cancellationToken),
            Times.Once);
    }
}
