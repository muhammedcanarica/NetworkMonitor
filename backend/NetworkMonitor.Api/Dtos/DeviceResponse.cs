using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Dtos;

public sealed record DeviceResponse(
    int Id,
    string Name,
    string IpAddress,
    string? Description,
    DeviceStatus Status,
    DateTimeOffset? LastSeenAt,
    DateTimeOffset? LastCheckedAt,
    long? LastLatencyMs,
    bool IsMonitoringEnabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static DeviceResponse FromDevice(Device device)
    {
        return new DeviceResponse(
            device.Id,
            device.Name,
            device.IpAddress,
            device.Description,
            device.Status,
            device.LastSeenAt,
            device.LastCheckedAt,
            device.LastLatencyMs,
            device.IsMonitoringEnabled,
            device.CreatedAt,
            device.UpdatedAt);
    }
}
