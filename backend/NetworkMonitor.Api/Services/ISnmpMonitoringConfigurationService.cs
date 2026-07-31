using NetworkMonitor.Api.Dtos;

namespace NetworkMonitor.Api.Services;

public interface ISnmpMonitoringConfigurationService
{
    Task<SnmpMonitoringProfileResponse?> GetAsync(int deviceId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SnmpInterfaceResponse>> DiscoverInterfacesAsync(int deviceId, DiscoverMonitoringInterfacesRequest request, CancellationToken cancellationToken);
    Task<SnmpMonitoringProfileResponse> UpdateAsync(int deviceId, UpdateSnmpMonitoringRequest request, CancellationToken cancellationToken);
    Task DisableAsync(int deviceId, CancellationToken cancellationToken);
    Task<IReadOnlyList<InterfaceTrafficSummaryResponse>> GetSummaryAsync(int deviceId, CancellationToken cancellationToken);
    Task<InterfaceTrafficHistoryResponse> GetHistoryAsync(int deviceId, int interfaceIndex, int hours, CancellationToken cancellationToken);
}

public sealed class SnmpMonitoringValidationException(string message) : ArgumentException(message);
public sealed class SnmpMonitoringNotFoundException(string message) : KeyNotFoundException(message);
