namespace NetworkMonitor.Api.Configuration;

public sealed class EmailNotificationDeliveryOptions
{
    public const string SectionName = "EmailNotificationDelivery";
    public int PollIntervalSeconds { get; init; } = 10;
    public int BatchSize { get; init; } = 20;
    public int MaxAttempts { get; init; } = 3;
}
