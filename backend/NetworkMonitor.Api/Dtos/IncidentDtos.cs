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
    DateTimeOffset StartedAt,
    DateTimeOffset? ResolvedAt,
    long DurationSeconds);
