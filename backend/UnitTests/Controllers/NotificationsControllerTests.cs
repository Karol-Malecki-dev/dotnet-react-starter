using API.Modules.Notifications.GetUnreadCount;
using Application.Modules.Notifications.GetUnreadCount;
using MediatR;
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
        var senderMock = new Mock<ISender>();
        senderMock
            .Setup(sender => sender.Send(
                It.Is<GetUnreadCountQuery>(query => query.UserId == userId),
                It.Is<CancellationToken>(token => token == cancellationToken)))
            .ReturnsAsync(ApiResponse<int>.Success(3));

        var controller = new GetUnreadCountController(senderMock.Object)
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
        senderMock.Verify(
            sender => sender.Send(It.IsAny<GetUnreadCountQuery>(), cancellationToken),
            Times.Once);
    }
}
