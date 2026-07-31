namespace NetworkMonitor.Api.Services;

public sealed class IncidentNotificationPublisher(
    INotificationService notificationService,
    ILogger<IncidentNotificationPublisher> logger) : IIncidentNotificationPublisher
{
    public async Task PublishOpenedAsync(long incidentId, CancellationToken cancellationToken)
    {
        try
        {
            await notificationService.CreateForIncidentAsync(incidentId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not create notification for incident {IncidentId}.", incidentId);
        }
    }
}
