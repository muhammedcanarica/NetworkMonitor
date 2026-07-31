namespace NetworkMonitor.Api.Models;

public sealed class SnmpMonitoringProfile
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public int CredentialId { get; set; }
    public bool IsEnabled { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Device Device { get; set; } = null!;
    public NetworkCredential Credential { get; set; } = null!;
    public ICollection<SnmpMonitoredInterface> Interfaces { get; set; } = new List<SnmpMonitoredInterface>();
}
