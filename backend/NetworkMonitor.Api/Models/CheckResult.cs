namespace NetworkMonitor.Api.Models;

public sealed class CheckResult
{
    public long Id { get; set; }

    public int DeviceId { get; set; }

    public DateTimeOffset CheckedAt { get; set; }

    public bool IsSuccess { get; set; }

    public long? LatencyMs { get; set; }

    public DeviceStatus DeviceStatus { get; set; }

    public string? FailureReason { get; set; }

    public Device Device { get; set; } = null!;

    public static CheckResult Create(
        int deviceId,
        DateTimeOffset checkedAt,
        PingCheckResult pingResult,
        DeviceStatus deviceStatus)
    {
        return new CheckResult
        {
            DeviceId = deviceId,
            CheckedAt = checkedAt,
            IsSuccess = pingResult.Success,
            LatencyMs = pingResult.Success ? pingResult.RoundtripTimeMs : null,
            DeviceStatus = deviceStatus,
            FailureReason = pingResult.Success
                ? null
                : PingFailureReasons.Normalize(pingResult.FailureReason)
        };
    }
}
