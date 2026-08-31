using API.Modules.Notifications.GetUnreadCount;
using Application.Modules.Notifications.GetUnreadCount;
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
        var handlerMock = new Mock<IGetUnreadCountHandler>();
        handlerMock
            .Setup(handler => handler.HandleAsync(
                It.Is<GetUnreadCountQuery>(query => query.UserId == userId),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .ReturnsAsync(ApiResponse<int>.Success(3));

        var controller = new GetUnreadCountController(handlerMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = ControllerTestHelper.CreateHttpContext(
                    ControllerTestHelper.CreateAuthenticatedUser(userId.ToString(), "user@test.com"))
            }
        };

        var actionResult = await controller.Get(cancellationToken);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        var response = Assert.IsType<ApiResponse<int>>(objectResult.Value);
        Assert.Equal(3, response.Data);
        handlerMock.Verify(
            handler => handler.HandleAsync(It.IsAny<GetUnreadCountQuery>(), cancellationToken),
            Times.Once);
    }
}
