namespace NetworkMonitor.Api.Models;

public sealed class Notification
{
    public long Id { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public long? IncidentId { get; set; }
    public int? DeviceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public Incident? Incident { get; set; }
    public Device? Device { get; set; }
}

public enum NotificationType { IncidentOpened }
