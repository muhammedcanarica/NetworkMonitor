namespace NetworkMonitor.Api.Services;

public sealed class IncidentNotificationPublisher(
    INotificationService notificationService,
    INotificationDeliveryPlanner deliveryPlanner,
    ILogger<IncidentNotificationPublisher> logger) : IIncidentNotificationPublisher
{
    public async Task PublishOpenedAsync(long incidentId, CancellationToken cancellationToken)
    {
        NetworkMonitor.Api.Dtos.NotificationResponse? notification;
        try
        {
            notification = await notificationService.CreateForIncidentAsync(incidentId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not create notification for incident {IncidentId}.", incidentId);
            return;
        }
        if (notification is null) return;
        try
        {
            await deliveryPlanner.ScheduleAsync(notification.Id, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not schedule delivery for notification {NotificationId}.", notification.Id);
        }
    }
}
