using NetworkMonitor.Api.Services;

namespace NetworkMonitor.Api.Tests.Infrastructure;

internal sealed class StubNotificationDeliveryPlanner : INotificationDeliveryPlanner
{
    public List<long> ScheduledNotificationIds { get; } = [];
    public Task ScheduleAsync(long notificationId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ScheduledNotificationIds.Add(notificationId);
        return Task.CompletedTask;
    }
}
