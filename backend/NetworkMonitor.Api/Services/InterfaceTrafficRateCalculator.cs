using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Services;

public static class InterfaceTrafficRateCalculator
{
    public static (double? InBitsPerSecond, double? OutBitsPerSecond) Calculate(
        InterfaceTrafficSample? previous,
        InterfaceCounterReading current,
        DateTimeOffset timestamp,
        TimeSpan maximumGap)
    {
        if (previous is null
            || timestamp <= previous.Timestamp
            || timestamp - previous.Timestamp > maximumGap
            || current.SysUpTimeTicks < previous.SysUpTimeTicks
            || current.InOctets < previous.InOctets
            || current.OutOctets < previous.OutOctets
            || DiscontinuityChanged(previous.CounterDiscontinuityTicks, current.CounterDiscontinuityTicks))
        {
            return (null, null);
        }

        var elapsedSeconds = (timestamp - previous.Timestamp).TotalSeconds;
        var inbound = (current.InOctets - previous.InOctets) * 8d / elapsedSeconds;
        var outbound = (current.OutOctets - previous.OutOctets) * 8d / elapsedSeconds;
        return (inbound, outbound);
    }

    private static bool DiscontinuityChanged(long? previous, long? current)
        => previous.HasValue && current.HasValue && previous.Value != current.Value;
}
