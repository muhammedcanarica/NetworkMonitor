namespace NetworkMonitor.Api.Models;

public sealed class Device
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string IpAddress { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DeviceStatus Status { get; set; } = DeviceStatus.Unknown;

    public DateTimeOffset? LastSeenAt { get; set; }

    public DateTimeOffset? LastCheckedAt { get; set; }

    public long? LastLatencyMs { get; set; }

    public bool IsMonitoringEnabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
