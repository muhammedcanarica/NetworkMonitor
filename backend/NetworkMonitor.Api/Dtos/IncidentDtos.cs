using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Dtos;

public sealed record IncidentResponse(
    long Id,
    int DeviceId,
    string DeviceName,
    string DeviceIpAddress,
    IncidentType Type,
    IncidentStatus Status,
    string Summary,
    int? InterfaceIndex,
    string? InterfaceName,
    BandwidthDirection? Direction,
    double? ThresholdBitsPerSecond,
    double? ObservedBitsPerSecond,
    DateTimeOffset StartedAt,
    DateTimeOffset? ResolvedAt,
    long DurationSeconds);
