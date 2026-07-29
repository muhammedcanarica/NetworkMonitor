namespace NetworkMonitor.Api.Dtos;

public sealed record DeviceMonitoringSummaryResponse(
    int TotalChecks,
    int SuccessfulChecks,
    int FailedChecks,
    double UptimePercentage,
    double? AverageLatencyMs,
    long? MinLatencyMs,
    long? MaxLatencyMs);
