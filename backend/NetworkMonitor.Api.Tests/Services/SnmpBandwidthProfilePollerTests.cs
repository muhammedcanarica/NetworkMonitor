using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NetworkMonitor.Api.Configuration;
using NetworkMonitor.Api.Models;
using NetworkMonitor.Api.Services;
using NetworkMonitor.Api.Tests.Infrastructure;

namespace NetworkMonitor.Api.Tests.Services;

public sealed class SnmpBandwidthProfilePollerTests
{
    [Fact]
    public async Task PollAsync_ReadsOnlyEnabledInterfacesAndPersistsBaselineThenRates()
    {
        await using var database = await TestDatabase.CreateAsync();
        var profile = await AddProfile(database, true);
        var probe = new FakeProbe([new InterfaceCounterReading(1, 1_000, 2_000, "Up", 10_000, 5)]);
        var poller = CreatePoller(database, probe);

        await poller.PollAsync(profile.Id, CancellationToken.None);
        var first = Assert.Single(database.Context.InterfaceTrafficSamples);
        Assert.Null(first.InBitsPerSecond);
        Assert.Equal([1], probe.LastIndexes);

        await Task.Delay(20);
        probe.Readings = [new InterfaceCounterReading(1, 2_000, 3_000, "Up", 10_100, 5)];
        await poller.PollAsync(profile.Id, CancellationToken.None);

        var samples = database.Context.InterfaceTrafficSamples.OrderBy(item => item.Timestamp).ToList();
        Assert.Equal(2, samples.Count);
        Assert.True(samples[1].InBitsPerSecond > 0);
        Assert.True(samples[1].OutBitsPerSecond > 0);
    }

    [Fact]
    public async Task PollAsync_DoesNotPollDisabledProfile()
    {
        await using var database = await TestDatabase.CreateAsync();
        var profile = await AddProfile(database, false);
        var probe = new FakeProbe([]);

        await CreatePoller(database, probe).PollAsync(profile.Id, CancellationToken.None);

        Assert.Equal(0, probe.CallCount);
        Assert.Empty(database.Context.InterfaceTrafficSamples);
    }

    [Fact]
    public async Task PollAsync_WrongCredentialOrDecryptionFailureDoesNotReachProbe()
    {
        await using var database = await TestDatabase.CreateAsync();
        var profile = await AddProfile(database, true);
        var probe = new FakeProbe([]);
        var resolver = new StubNetworkOperationCredentialResolver
        {
            SnmpHandler = (_, _, _) => throw new NetworkOperationCredentialException("Saved credential could not be used.")
        };
        var poller = new SnmpBandwidthProfilePoller(database.Context, resolver, probe, Options.Create(OptionsValue()), NullLogger<SnmpBandwidthProfilePoller>.Instance);

        await Assert.ThrowsAsync<NetworkOperationCredentialException>(() => poller.PollAsync(profile.Id, CancellationToken.None));
        Assert.Equal(0, probe.CallCount);
    }

    private static SnmpBandwidthProfilePoller CreatePoller(TestDatabase database, FakeProbe probe)
        => new(database.Context, new StubNetworkOperationCredentialResolver { SnmpHandler = (_, _, _) => Task.FromResult("private") }, probe, Options.Create(OptionsValue()), NullLogger<SnmpBandwidthProfilePoller>.Instance);

    private static SnmpBandwidthMonitoringOptions OptionsValue() => new() { IntervalSeconds = 60, MaxConcurrentDevices = 2, HistoryRetentionDays = 7, RequestTimeoutMilliseconds = 2000 };

    private static async Task<SnmpMonitoringProfile> AddProfile(TestDatabase database, bool enabled)
    {
        var device = await database.AddDeviceAsync();
        var credential = new NetworkCredential { Name = "SNMP", Type = NetworkCredentialType.SnmpV2Community, ProtectedSecret = "protected", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var profile = new SnmpMonitoringProfile
        {
            DeviceId = device.Id, Credential = credential, IsEnabled = enabled, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
            Interfaces =
            [
                new SnmpMonitoredInterface { InterfaceIndex = 1, InterfaceName = "eth0", IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow },
                new SnmpMonitoredInterface { InterfaceIndex = 2, InterfaceName = "eth1", IsEnabled = false, CreatedAt = DateTimeOffset.UtcNow }
            ]
        };
        database.Context.Add(profile);
        await database.Context.SaveChangesAsync();
        return profile;
    }

    private sealed class FakeProbe(IReadOnlyList<InterfaceCounterReading> readings) : ISnmpBandwidthProbe
    {
        public IReadOnlyList<InterfaceCounterReading> Readings { get; set; } = readings;
        public int CallCount { get; private set; }
        public IReadOnlyList<int> LastIndexes { get; private set; } = [];
        public Task<IReadOnlyList<InterfaceCounterReading>> ReadAsync(string ipAddress, string community, IReadOnlyList<int> interfaceIndexes, int timeoutMilliseconds, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastIndexes = interfaceIndexes;
            return Task.FromResult(Readings);
        }
    }
}
