using System.ComponentModel.DataAnnotations;
using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Dtos;

public sealed class CreateNetworkCredentialRequest
{
    [Required, StringLength(100)] public string Name { get; init; } = string.Empty;
    public NetworkCredentialType Type { get; init; }
    [StringLength(100)] public string? Username { get; init; }
    [Required, StringLength(1024)] public string Secret { get; init; } = string.Empty;
    public int? DeviceId { get; init; }
    public override string ToString() => $"Create credential {Name} ({Type}), secret [REDACTED]";
}

public sealed class UpdateNetworkCredentialRequest
{
    [Required, StringLength(100)] public string Name { get; init; } = string.Empty;
    public NetworkCredentialType Type { get; init; }
    [StringLength(100)] public string? Username { get; init; }
    [StringLength(1024)] public string? Secret { get; init; }
    public int? DeviceId { get; init; }
    public override string ToString() => $"Update credential {Name} ({Type}), secret {(string.IsNullOrWhiteSpace(Secret) ? "unchanged" : "[REDACTED]")}";
}

public sealed record NetworkCredentialResponse(int Id, string Name, NetworkCredentialType Type, string? Username, int? DeviceId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, bool HasSecret);
