using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Dtos;

public sealed record DeviceMonitoringUpdate(
    int DeviceId,
    DeviceStatus Status,
    DateTimeOffset? LastCheckedAt,
    DateTimeOffset? LastSeenAt,
    long? LastLatencyMs,
    bool IsMonitoringEnabled)
{
    public static DeviceMonitoringUpdate FromDevice(Device device)
    {
        return new DeviceMonitoringUpdate(
            device.Id,
            device.Status,
            device.LastCheckedAt,
            device.LastSeenAt,
            device.LastLatencyMs,
            device.IsMonitoringEnabled);
    }
}
