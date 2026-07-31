using System.ComponentModel.DataAnnotations;
using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Dtos;

public sealed class TopologyDiscoveryRequest
{
    [Required]
    [MinLength(1)]
    public IReadOnlyList<int> DeviceIds { get; init; } = [];

    [Required(AllowEmptyStrings = false)]
    [StringLength(255, MinimumLength = 1)]
    public string Community { get; init; } = string.Empty;

    [Range(SnmpServiceTimeouts.MinimumMilliseconds, SnmpServiceTimeouts.MaximumMilliseconds)]
    public int TimeoutMilliseconds { get; init; } = 2000;

    public override string ToString() => $"Topology discovery for {DeviceIds.Count} devices, community [REDACTED], timeout {TimeoutMilliseconds} ms";
}

public sealed record TopologyNodeResponse(
    string Id,
    int? DeviceId,
    string? IpAddress,
    string Name,
    DeviceStatus? Status,
    bool IsManaged);

public sealed record TopologyEdgeResponse(
    string Id,
    string SourceNodeId,
    string TargetNodeId,
    string? LocalPort,
    string? RemotePort,
    string DiscoveryProtocol);

public sealed record TopologyDiscoveryResponse(
    IReadOnlyList<TopologyNodeResponse> Nodes,
    IReadOnlyList<TopologyEdgeResponse> Edges,
    int ScannedDevices,
    int SuccessfulDevices,
    int FailedDevices,
    long DurationMs,
    IReadOnlyList<string> Warnings);
