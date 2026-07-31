namespace NetworkMonitor.Api.Models;

public sealed class InterfaceTrafficSample
{
    public long Id { get; set; }
    public int SnmpMonitoredInterfaceId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public long InOctets { get; set; }
    public long OutOctets { get; set; }
    public double? InBitsPerSecond { get; set; }
    public double? OutBitsPerSecond { get; set; }
    public string OperStatus { get; set; } = "Unknown";
    public long SysUpTimeTicks { get; set; }
    public long? CounterDiscontinuityTicks { get; set; }
    public SnmpMonitoredInterface MonitoredInterface { get; set; } = null!;
}
