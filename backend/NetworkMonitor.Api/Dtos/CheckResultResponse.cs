using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Dtos;

public sealed record CheckResultResponse(
    long Id,
    int DeviceId,
    DateTimeOffset CheckedAt,
    bool IsSuccess,
    long? LatencyMs,
    DeviceStatus DeviceStatus,
    string? FailureReason);
