using System.ComponentModel.DataAnnotations;

namespace NetworkMonitor.Api.Dtos;

public sealed class UpdateDeviceRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [StringLength(45)]
    [ValidIpAddress]
    public string IpAddress { get; init; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; init; }

    [Required]
    public bool? IsMonitoringEnabled { get; init; }
}
