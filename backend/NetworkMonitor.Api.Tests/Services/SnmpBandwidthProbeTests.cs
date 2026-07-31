using NetworkMonitor.Api.Models;
using NetworkMonitor.Api.Services;

namespace NetworkMonitor.Api.Tests.Services;

public sealed class SnmpBandwidthProbeTests
{
    [Fact]
    public async Task ReadAsync_UsesFixedHighCapacityOidsAndMapsSelectedInterface()
    {
        var transport = new ProbeTransport((_, oids, _) =>
        {
            if (oids.Count == 1 && oids[0] == SnmpOids.System.UpTime)
                return Task.FromResult<IReadOnlyList<SnmpVariableValue>>([new(SnmpOids.System.UpTime, "10000", "TimeTicks", 10_000)]);
            Assert.Contains($"{SnmpOids.Interfaces.HighCapacityInOctets}.7", oids);
            Assert.Contains($"{SnmpOids.Interfaces.HighCapacityOutOctets}.7", oids);
            Assert.DoesNotContain($"{SnmpOids.Interfaces.InOctets}.7", oids);
            return Task.FromResult<IReadOnlyList<SnmpVariableValue>>([
                new($"{SnmpOids.Interfaces.HighCapacityInOctets}.7", "1000", "Counter64", 1_000),
                new($"{SnmpOids.Interfaces.HighCapacityOutOctets}.7", "2000", "Counter64", 2_000),
                new($"{SnmpOids.Interfaces.AdminStatus}.7", "1", "Integer32", 1),
                new($"{SnmpOids.Interfaces.OperStatus}.7", "1", "Integer32", 1),
                new($"{SnmpOids.Interfaces.CounterDiscontinuityTime}.7", "5", "TimeTicks", 5)
            ]);
        });

        var reading = Assert.Single(await new SnmpBandwidthProbe(transport).ReadAsync("192.0.2.1", "private", [7], 2000, CancellationToken.None));

        Assert.Equal(1_000, reading.InOctets);
        Assert.Equal(2_000, reading.OutOctets);
        Assert.Equal("Up", reading.AdminStatus);
        Assert.Equal("Up", reading.OperStatus);
    }

    [Fact]
    public async Task ReadAsync_MissingHighCapacityCountersDoesNotInventFallbackData()
    {
        var transport = new ProbeTransport((_, oids, _) => Task.FromResult<IReadOnlyList<SnmpVariableValue>>(
            oids.Count == 1 ? [new(SnmpOids.System.UpTime, "10000", "TimeTicks", 10_000)] : []));

        var readings = await new SnmpBandwidthProbe(transport).ReadAsync("192.0.2.1", "private", [7], 2000, CancellationToken.None);

        Assert.Empty(readings);
    }

    private sealed class ProbeTransport(Func<SnmpConnection, IReadOnlyList<string>, CancellationToken, Task<IReadOnlyList<SnmpVariableValue>>> get) : ISnmpTransport
    {
        public Task<IReadOnlyList<SnmpVariableValue>> GetAsync(SnmpConnection connection, IReadOnlyList<string> oids, CancellationToken cancellationToken) => get(connection, oids, cancellationToken);
        public Task<IReadOnlyList<SnmpVariableValue>> WalkAsync(SnmpConnection connection, string rootOid, int maxResults, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
