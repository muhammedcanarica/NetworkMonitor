using NetworkMonitor.Api.Dtos;

namespace NetworkMonitor.Api.Models;

public sealed class ConfigBackup
{
    public int Id { get; set; }

    public int? DeviceId { get; set; }

    public string IpAddress { get; set; } = string.Empty;

    public ConfigBackupVendor Vendor { get; set; }

    public string Configuration { get; set; } = string.Empty;

    public DateTimeOffset CapturedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string Hash { get; set; } = string.Empty;
}
