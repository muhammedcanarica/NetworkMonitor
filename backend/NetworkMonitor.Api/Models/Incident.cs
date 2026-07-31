namespace NetworkMonitor.Api.Models;

public sealed class Incident
{
    public long Id { get; set; }
    public int DeviceId { get; set; }
    public IncidentStatus Status { get; set; }
    public IncidentType Type { get; set; }
    public string Summary { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Device Device { get; set; } = null!;
}

public enum IncidentStatus { Open, Resolved }

public enum IncidentType { DeviceUnreachable }
