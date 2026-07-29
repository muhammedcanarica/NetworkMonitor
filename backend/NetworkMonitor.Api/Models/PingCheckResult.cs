namespace NetworkMonitor.Api.Models;

public sealed record PingCheckResult(bool Success, long? RoundtripTimeMs, string? Error)
{
    public static PingCheckResult Succeeded(long roundtripTimeMs)
    {
        return new PingCheckResult(true, roundtripTimeMs, null);
    }

    public static PingCheckResult Failed(string error)
    {
        return new PingCheckResult(false, null, error);
    }
}
