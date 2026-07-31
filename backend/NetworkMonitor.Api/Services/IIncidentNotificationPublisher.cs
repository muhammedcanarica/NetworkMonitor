namespace NetworkMonitor.Api.Services;

public interface IIncidentNotificationPublisher
{
    Task PublishOpenedAsync(long incidentId, CancellationToken cancellationToken);
}
