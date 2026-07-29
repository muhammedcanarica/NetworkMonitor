namespace NetworkMonitor.Api.Models;

public sealed record PingCheckResult(bool Success, long? RoundtripTimeMs, string? FailureReason)
{
    public static PingCheckResult Succeeded(long roundtripTimeMs)
    {
        return new PingCheckResult(true, roundtripTimeMs, null);
    }

    public static PingCheckResult Failed(string failureReason)
    {
        return new PingCheckResult(false, null, PingFailureReasons.Normalize(failureReason));
    }
}
