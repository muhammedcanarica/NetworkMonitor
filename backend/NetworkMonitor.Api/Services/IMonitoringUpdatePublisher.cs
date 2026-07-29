using NetworkMonitor.Api.Dtos;

namespace NetworkMonitor.Api.Services;

public interface IMonitoringUpdatePublisher
{
    Task PublishAsync(DeviceMonitoringUpdate update, CancellationToken cancellationToken);
}
