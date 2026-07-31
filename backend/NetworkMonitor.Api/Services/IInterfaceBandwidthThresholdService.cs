using NetworkMonitor.Api.Dtos;

namespace NetworkMonitor.Api.Services;

public interface IInterfaceBandwidthThresholdService
{
    Task<InterfaceBandwidthThresholdResponse?> GetAsync(int deviceId, int interfaceIndex, CancellationToken cancellationToken);
    Task<InterfaceBandwidthThresholdResponse> UpdateAsync(int deviceId, int interfaceIndex, UpdateInterfaceBandwidthThresholdRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(int deviceId, int interfaceIndex, CancellationToken cancellationToken);
}

public sealed class InterfaceBandwidthThresholdValidationException(string message) : ArgumentException(message);
public sealed class InterfaceBandwidthThresholdNotFoundException(string message) : KeyNotFoundException(message);
public sealed class InterfaceBandwidthThresholdConflictException(string message) : InvalidOperationException(message);
