namespace NetworkMonitor.Api.Configuration;

public sealed class MonitoringOptions
{
    public const string SectionName = "Monitoring";

    public int IntervalSeconds { get; init; } = 5;

    public int PingTimeoutMilliseconds { get; init; } = 2000;

    public int FailureThreshold { get; init; } = 3;

    public int RecoveryThreshold { get; init; } = 2;

    public int MaxConcurrentPings { get; init; } = 10;

    public int HistoryRetentionDays { get; init; } = 7;
}
