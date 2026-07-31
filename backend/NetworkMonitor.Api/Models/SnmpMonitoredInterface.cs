namespace NetworkMonitor.Api.Models;

public sealed class SnmpMonitoredInterface
{
    public int Id { get; set; }
    public int SnmpMonitoringProfileId { get; set; }
    public int InterfaceIndex { get; set; }
    public string InterfaceName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public InterfaceOperationalState? LastOperationalState { get; set; }
    public int ConsecutiveDownSamples { get; set; }
    public int ConsecutiveUpSamples { get; set; }
    public SnmpMonitoringProfile Profile { get; set; } = null!;
    public InterfaceBandwidthThreshold? BandwidthThreshold { get; set; }
    public ICollection<InterfaceTrafficSample> Samples { get; set; } = new List<InterfaceTrafficSample>();
}

public enum InterfaceOperationalState { Up, Problem, Neutral }
