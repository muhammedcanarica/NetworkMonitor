namespace NetworkMonitor.Api.Models;

public sealed class InterfaceBandwidthThreshold
{
    public int Id { get; set; }
    public int SnmpMonitoredInterfaceId { get; set; }
    public double? InboundThresholdBitsPerSecond { get; set; }
    public double? OutboundThresholdBitsPerSecond { get; set; }
    public int BreachSampleCount { get; set; } = 3;
    public int RecoverySampleCount { get; set; } = 2;
    public bool IsEnabled { get; set; } = true;
    public int InboundConsecutiveBreaches { get; set; }
    public int OutboundConsecutiveBreaches { get; set; }
    public int InboundConsecutiveRecoveries { get; set; }
    public int OutboundConsecutiveRecoveries { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public SnmpMonitoredInterface MonitoredInterface { get; set; } = null!;
}
