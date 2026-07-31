namespace NetworkMonitor.Api.Services;

public interface INotificationDeliveryPlanner
{
    Task ScheduleAsync(long notificationId, CancellationToken cancellationToken);
}
