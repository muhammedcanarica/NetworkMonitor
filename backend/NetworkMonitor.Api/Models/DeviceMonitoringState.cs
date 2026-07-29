namespace NetworkMonitor.Api.Models;

public sealed record DeviceMonitoringState(
    DeviceStatus Status,
    int ConsecutiveFailures,
    int ConsecutiveSuccesses);
