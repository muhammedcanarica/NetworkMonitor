using NetworkMonitor.Api.Models;
using NetworkMonitor.Api.Services;

namespace NetworkMonitor.Api.Tests.Services;

public sealed class InterfaceTrafficRateCalculatorTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 7, 31, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Calculate_FirstSampleCreatesBaseline()
    {
        var rates = InterfaceTrafficRateCalculator.Calculate(null, Reading(1_000, 2_000), Timestamp, TimeSpan.FromMinutes(3));

        Assert.Null(rates.InBitsPerSecond);
        Assert.Null(rates.OutBitsPerSecond);
    }

    [Fact]
    public void Calculate_UsesCounterDeltaAndElapsedTime()
    {
        var previous = Sample(1_000, 2_000, Timestamp);

        var rates = InterfaceTrafficRateCalculator.Calculate(previous, Reading(2_000, 2_500), Timestamp.AddSeconds(10), TimeSpan.FromMinutes(3));

        Assert.Equal(800, rates.InBitsPerSecond);
        Assert.Equal(400, rates.OutBitsPerSecond);
    }

    [Fact]
    public void Calculate_UsesDifferentElapsedTime()
    {
        var rates = InterfaceTrafficRateCalculator.Calculate(Sample(1_000, 1_000, Timestamp), Reading(2_000, 3_000), Timestamp.AddSeconds(20), TimeSpan.FromMinutes(3));

        Assert.Equal(400, rates.InBitsPerSecond);
        Assert.Equal(800, rates.OutBitsPerSecond);
    }

    [Theory]
    [InlineData("counter")]
    [InlineData("uptime")]
    [InlineData("discontinuity")]
    [InlineData("gap")]
    public void Calculate_ResetOrLongGapCreatesNewBaseline(string reason)
    {
        var previous = Sample(1_000, 2_000, Timestamp);
        var reading = reason switch
        {
            "counter" => Reading(999, 2_500),
            "uptime" => Reading(2_000, 2_500) with { SysUpTimeTicks = 9_000 },
            "discontinuity" => Reading(2_000, 2_500) with { CounterDiscontinuityTicks = 6 },
            _ => Reading(2_000, 2_500)
        };
        var currentTimestamp = reason == "gap" ? Timestamp.AddMinutes(4) : Timestamp.AddSeconds(60);

        var rates = InterfaceTrafficRateCalculator.Calculate(previous, reading, currentTimestamp, TimeSpan.FromMinutes(3));

        Assert.Null(rates.InBitsPerSecond);
        Assert.Null(rates.OutBitsPerSecond);
    }

    private static InterfaceCounterReading Reading(long inbound, long outbound) => new(1, inbound, outbound, "Up", "Up", 10_000, 5);
    private static InterfaceTrafficSample Sample(long inbound, long outbound, DateTimeOffset timestamp) => new() { Timestamp = timestamp, InOctets = inbound, OutOctets = outbound, SysUpTimeTicks = 10_000, CounterDiscontinuityTicks = 5 };
}
