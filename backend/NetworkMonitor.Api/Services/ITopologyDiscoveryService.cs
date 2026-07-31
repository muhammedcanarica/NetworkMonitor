using NetworkMonitor.Api.Dtos;

namespace NetworkMonitor.Api.Services;

public interface ITopologyDiscoveryService
{
    Task<TopologyDiscoveryResponse> DiscoverAsync(
        TopologyDiscoveryRequest request,
        CancellationToken cancellationToken);
}
