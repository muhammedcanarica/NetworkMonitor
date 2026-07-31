using NetworkMonitor.Api.Services;

namespace NetworkMonitor.Api.Tests.Infrastructure;

internal sealed class StubIncidentNotificationPublisher : IIncidentNotificationPublisher
{
    public List<long> PublishedIncidentIds { get; } = [];

    public Task PublishOpenedAsync(long incidentId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PublishedIncidentIds.Add(incidentId);
        return Task.CompletedTask;
    }
}
