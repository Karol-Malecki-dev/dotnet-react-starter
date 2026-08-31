using Application.DTOs.Admin;
using Domain.Entities;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace UnitTests.Infrastructure;

public sealed class DatabaseAdminSecurityEventTests
{
    [Fact]
    public async Task Service_filters_events_and_returns_pagination_metadata()
    {
        var subjectUserId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nameof(Service_filters_events_and_returns_pagination_metadata))
            .Options;
        await using var context = new ApplicationDbContext(options);
        context.AccountSecurityEvents.AddRange(
            AccountSecurityEvent.Create("auth.login.failed", "failure", subjectUserId: subjectUserId, occurredAt: new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc), correlationId: "corr-1"),
            AccountSecurityEvent.Create("auth.login.succeeded", "success", subjectUserId: subjectUserId, occurredAt: new DateTime(2026, 8, 2, 10, 0, 0, DateTimeKind.Utc), correlationId: "corr-2"),
            AccountSecurityEvent.Create("auth.login.failed", "failure", occurredAt: new DateTime(2026, 8, 3, 10, 0, 0, DateTimeKind.Utc), correlationId: "corr-3"));
        await context.SaveChangesAsync();

        var service = new DatabaseAdminService(context);
        var result = await service.GetAccountSecurityEventsAsync(new AdminAccountSecurityEventFilterRequestDto
        {
            EventCode = " auth.login.failed ",
            Outcome = "failure",
            SubjectUserId = subjectUserId,
            From = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            To = new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc),
            PageNumber = 1,
            PageSize = 1
        });

        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data!.Items);
        Assert.Equal(1, result.Data.TotalCount);
        Assert.Equal(1, result.Data.TotalPages);
        Assert.Equal("corr-1", result.Data.Items[0].CorrelationId);
    }

    [Fact]
    public async Task Service_rejects_unbounded_page_size_and_invalid_date_range()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(nameof(Service_rejects_unbounded_page_size_and_invalid_date_range))
            .Options;
        await using var context = new ApplicationDbContext(options);
        var service = new DatabaseAdminService(context);

        var pageResult = await service.GetAccountSecurityEventsAsync(new AdminAccountSecurityEventFilterRequestDto { PageSize = 21 });
        var dateResult = await service.GetAccountSecurityEventsAsync(new AdminAccountSecurityEventFilterRequestDto
        {
            From = DateTime.UtcNow,
            To = DateTime.UtcNow.AddDays(-1)
        });

        Assert.Equal(400, pageResult.StatusCode);
        Assert.Equal(400, dateResult.StatusCode);
    }
}