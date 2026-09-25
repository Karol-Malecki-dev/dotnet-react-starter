using Application.Modules.Notifications.GetUnreadCount;
using Infrastructure.Modules.Notifications.GetUnreadCount;
using Moq;

namespace UnitTests.Modules.Notifications.GetUnreadCount;

public sealed class GetUnreadCountHandlerTests
{
    [Fact]
    public async Task Handle_returns_store_value_and_forwards_cancellation()
    {
        var userId = Guid.NewGuid();
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var store = new Mock<IGetUnreadCountStore>();
        store
            .Setup(item => item.QueryAsync(userId, cancellationToken))
            .ReturnsAsync(4);

        var result = await new GetUnreadCountHandler(store.Object)
            .Handle(new GetUnreadCountQuery(userId), cancellationToken);

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(4, result.Data);
        store.Verify(item => item.QueryAsync(userId, cancellationToken), Times.Once);
    }
}
