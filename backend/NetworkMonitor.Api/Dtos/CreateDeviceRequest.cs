using System.ComponentModel.DataAnnotations;

namespace NetworkMonitor.Api.Dtos;

public sealed class CreateDeviceRequest
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
}
