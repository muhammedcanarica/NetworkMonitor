using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Services;

public interface IIncidentService
{
    Task HandleStatusTransitionAsync(int deviceId, DeviceStatus previousStatus, DeviceStatus currentStatus, CancellationToken cancellationToken);
}
