using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Services;

public interface IInterfaceBandwidthThresholdEvaluator
{
    Task EvaluateAsync(int monitoredInterfaceId, InterfaceTrafficSample sample, CancellationToken cancellationToken);
}
