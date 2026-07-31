using Microsoft.AspNetCore.Mvc;
using NetworkMonitor.Api.Controllers;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Models;
using NetworkMonitor.Api.Services;
using NetworkMonitor.Api.Tests.Infrastructure;

namespace NetworkMonitor.Api.Tests.Controllers;

public sealed class NotificationsControllerTests
{
    [Fact]
    public async Task Endpoints_ListCountMarkReadAndMarkAll()
    {
        await using var database = await TestDatabase.CreateAsync();
        var device = await database.AddDeviceAsync();
        database.Context.Notifications.AddRange(
            NewNotification(device.Id, DateTimeOffset.UtcNow.AddMinutes(-1)),
            NewNotification(device.Id, DateTimeOffset.UtcNow));
        await database.Context.SaveChangesAsync();
        var controller = new NotificationsController(new NotificationService(database.Context));

        var listAction = await controller.GetAll(false, 50, CancellationToken.None);
        var list = Assert.IsType<OkObjectResult>(listAction.Result).Value as IReadOnlyList<NotificationResponse>;
        Assert.Equal(2, list!.Count);
        Assert.True(list[0].CreatedAt >= list[1].CreatedAt);

        var countAction = await controller.GetUnreadCount(CancellationToken.None);
        var count = Assert.IsType<NotificationUnreadCountResponse>(Assert.IsType<OkObjectResult>(countAction.Result).Value);
        Assert.Equal(2, count.Count);

        Assert.IsType<NoContentResult>(await controller.MarkAsRead(list[0].Id, CancellationToken.None));
        Assert.IsType<NoContentResult>(await controller.MarkAsRead(list[0].Id, CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.MarkAsRead(999, CancellationToken.None));
        Assert.IsType<NoContentResult>(await controller.MarkAllAsRead(CancellationToken.None));
        Assert.Equal(0, (await new NotificationService(database.Context).GetUnreadCountAsync(CancellationToken.None)));
    }

    private static Notification NewNotification(int deviceId, DateTimeOffset createdAt) => new()
    {
        Type = NotificationType.IncidentOpened,
        Title = "Device unreachable",
        Message = "A device became unreachable.",
        DeviceId = deviceId,
        CreatedAt = createdAt
    };
}
