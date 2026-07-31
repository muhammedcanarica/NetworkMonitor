namespace NetworkMonitor.Api.Configuration;

public sealed class SnmpBandwidthMonitoringOptions
{
    public const string SectionName = "SnmpBandwidthMonitoring";
    public const int MinimumIntervalSeconds = 15;
    public const int MaximumIntervalSeconds = 3600;

    public int IntervalSeconds { get; init; } = 60;
    public int MaxConcurrentDevices { get; init; } = 4;
    public int HistoryRetentionDays { get; init; } = 7;
    public int RequestTimeoutMilliseconds { get; init; } = 5000;
    public int InterfaceDownTriggerSamples { get; init; } = 2;
    public int InterfaceUpRecoverySamples { get; init; } = 2;
}
