using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using NetworkMonitor.Api.Configuration;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Models;
using NetworkMonitor.Api.Services;
using NetworkMonitor.Api.Tests.Infrastructure;

namespace NetworkMonitor.Api.Tests.Services;

public sealed class TopologyDiscoveryServiceTests
{
    [Fact]
    public async Task DiscoverAsync_MatchesNeighborByManagementIpAndNormalizesReciprocalEdge()
    {
        await using var database = await TestDatabase.CreateAsync();
        var first = await database.AddDeviceAsync("Switch A", "192.0.2.1");
        var second = await database.AddDeviceAsync("Switch B", "192.0.2.2");
        var service = CreateService(database, new FakeSnmpService((ip, root, _) =>
            Task.FromResult(LldpRows(ip, root, ip == first.IpAddress ? "192.0.2.2" : "192.0.2.1", ip == first.IpAddress ? "Gi0/1" : "Gi0/24", ip == first.IpAddress ? "Gi0/24" : "Gi0/1"))));

        var result = await service.DiscoverAsync(CreateRequest(first.Id, second.Id), CancellationToken.None);

        Assert.Equal(2, result.Nodes.Count);
        var edge = Assert.Single(result.Edges);
        Assert.Equal("LLDP", edge.DiscoveryProtocol);
        Assert.Equal(2, result.SuccessfulDevices);
    }

    [Fact]
    public async Task DiscoverAsync_CreatesUnmanagedNeighborWhenManagementIpDoesNotMatch()
    {
        await using var database = await TestDatabase.CreateAsync();
        var device = await database.AddDeviceAsync("Core", "192.0.2.1");
        var service = CreateService(database, new FakeSnmpService((_, root, _) =>
            Task.FromResult(LldpRows("192.0.2.1", root, "198.51.100.7", "Gi0/1", "Gi0/24", "access-switch"))));

        var result = await service.DiscoverAsync(CreateRequest(device.Id), CancellationToken.None);

        var neighbor = Assert.Single(result.Nodes, node => !node.IsManaged);
        Assert.Equal("198.51.100.7", neighbor.IpAddress);
        Assert.Equal("access-switch", neighbor.Name);
        Assert.Single(result.Edges);
    }

    [Fact]
    public async Task DiscoverAsync_UsesAvailableLldpFieldsWhenSystemNameAndManagementAddressAreMissing()
    {
        await using var database = await TestDatabase.CreateAsync();
        var device = await database.AddDeviceAsync("Core", "192.0.2.1");
        var service = CreateService(database, new FakeSnmpService((_, root, _) => Task.FromResult<IReadOnlyList<SnmpValueResponse>>(
            root is SnmpOids.Lldp.RemoteSystemName or SnmpOids.Lldp.RemoteManagementAddress
                ? []
                : LldpRows("192.0.2.1", root, "198.51.100.7", "Gi0/1", "Gi0/24"))));

        var result = await service.DiscoverAsync(CreateRequest(device.Id), CancellationToken.None);

        var neighbor = Assert.Single(result.Nodes, node => !node.IsManaged);
        Assert.Equal("chassis-198.51.100.7", neighbor.Name);
        Assert.Null(neighbor.IpAddress);
    }

    [Fact]
    public async Task DiscoverAsync_ReturnsSelectedNodeWhenLldpTableIsEmpty()
    {
        await using var database = await TestDatabase.CreateAsync();
        var device = await database.AddDeviceAsync("Core", "192.0.2.1");
        var service = CreateService(database, new FakeSnmpService((_, _, _) => Task.FromResult<IReadOnlyList<SnmpValueResponse>>([])));

        var result = await service.DiscoverAsync(CreateRequest(device.Id), CancellationToken.None);

        Assert.Single(result.Nodes);
        Assert.Empty(result.Edges);
        Assert.Equal(1, result.SuccessfulDevices);
    }

    [Fact]
    public async Task DiscoverAsync_PreservesTwoPhysicalLinksWithDifferentPorts()
    {
        await using var database = await TestDatabase.CreateAsync();
        var source = await database.AddDeviceAsync("Source", "192.0.2.1");
        var target = await database.AddDeviceAsync("Target", "192.0.2.2");
        var service = CreateService(database, new FakeSnmpService((_, root, _) =>
            Task.FromResult<IReadOnlyList<SnmpValueResponse>>(LldpRows("192.0.2.1", root, target.IpAddress, "Gi0/1", "Gi0/24")
                .Concat(LldpRows("192.0.2.1", root, target.IpAddress, "Gi0/2", "Gi0/23", index: 2)).ToList())));

        var result = await service.DiscoverAsync(CreateRequest(source.Id), CancellationToken.None);

        Assert.Equal(2, result.Edges.Count);
    }

    [Fact]
    public async Task DiscoverAsync_ReturnsPartialResultWhenOneDeviceTimesOut()
    {
        await using var database = await TestDatabase.CreateAsync();
        var working = await database.AddDeviceAsync("Working", "192.0.2.1");
        var unavailable = await database.AddDeviceAsync("Unavailable", "192.0.2.2");
        var service = CreateService(database, new FakeSnmpService((ip, root, _) =>
            ip == unavailable.IpAddress
                ? throw new SnmpOperationException(SnmpErrorKind.Timeout, "community must not leak")
                : Task.FromResult(LldpRows(ip, root, "198.51.100.7", "Gi0/1", "Gi0/24"))));

        var result = await service.DiscoverAsync(CreateRequest(working.Id, unavailable.Id), CancellationToken.None);

        Assert.Equal(1, result.SuccessfulDevices);
        Assert.Equal(1, result.FailedDevices);
        Assert.DoesNotContain(result.Warnings, warning => warning.Contains("community", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DiscoverAsync_UsesBoundedConcurrencyAndRespectsCancellation()
    {
        await using var database = await TestDatabase.CreateAsync();
        var devices = new List<Device>();
        for (var index = 0; index < 6; index++) devices.Add(await database.AddDeviceAsync($"Switch {index}", $"192.0.2.{index + 1}"));
        var active = 0;
        var peak = 0;
        var service = CreateService(database, new FakeSnmpService(async (_, root, token) =>
        {
            var current = Interlocked.Increment(ref active);
            InterlockedExtensions.Max(ref peak, current);
            try
            {
                await Task.Delay(25, token);
                return LldpRows("192.0.2.1", root, "198.51.100.7", "Gi0/1", "Gi0/24");
            }
            finally { Interlocked.Decrement(ref active); }
        }), maxConcurrent: 2);

        var result = await service.DiscoverAsync(CreateRequest(devices.Select(device => device.Id).ToArray()), CancellationToken.None);
        Assert.True(peak <= 12); // two devices each start at most six fixed LLDP walks
        Assert.Equal(6, result.SuccessfulDevices);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.DiscoverAsync(CreateRequest(devices[0].Id), cancellation.Token));
    }

    [Fact]
    public async Task DiscoverAsync_RejectsInvalidDeviceSelectionAndLimit()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database, new FakeSnmpService((_, _, _) => Task.FromResult<IReadOnlyList<SnmpValueResponse>>([])), maxDevices: 1);

        await Assert.ThrowsAsync<TopologyDiscoveryValidationException>(() => service.DiscoverAsync(CreateRequest(999), CancellationToken.None));
        await Assert.ThrowsAsync<TopologyDiscoveryValidationException>(() => service.DiscoverAsync(CreateRequest(1, 2), CancellationToken.None));
    }

    private static TopologyDiscoveryService CreateService(TestDatabase database, FakeSnmpService snmp, int maxDevices = 32, int maxConcurrent = 4) => new(
        database.Context,
        snmp,
        Options.Create(new TopologyDiscoveryOptions { MaxDevicesPerDiscovery = maxDevices, MaxConcurrentDiscoveries = maxConcurrent }));

    private static TopologyDiscoveryRequest CreateRequest(params int[] deviceIds) => new()
    {
        DeviceIds = deviceIds,
        Community = "private-community",
        TimeoutMilliseconds = 1000
    };

    private static IReadOnlyList<SnmpValueResponse> LldpRows(string sourceIp, string root, string neighborIp, string localPort, string remotePort, string? name = null, int index = 1)
    {
        var remoteIndex = $"0.{index}.{index}";
        var value = root switch
        {
            SnmpOids.Lldp.LocalPortId => localPort,
            SnmpOids.Lldp.RemoteChassisId => $"chassis-{neighborIp}",
            SnmpOids.Lldp.RemotePortId => remotePort,
            SnmpOids.Lldp.RemotePortDescription => remotePort,
            SnmpOids.Lldp.RemoteSystemName => name ?? $"switch-{neighborIp}",
            SnmpOids.Lldp.RemoteManagementAddress => neighborIp,
            _ => null
        };
        var suffix = root == SnmpOids.Lldp.LocalPortId ? $".{index}" : $".{remoteIndex}";
        return value is null ? [] : [new SnmpValueResponse(root + suffix, value, "OctetString")];
    }

    private sealed class FakeSnmpService(Func<string, string, CancellationToken, Task<IReadOnlyList<SnmpValueResponse>>> walk) : ISnmpService
    {
        public Task<SnmpValueResponse> GetAsync(string ipAddress, string community, string oid, int timeoutMilliseconds, CancellationToken cancellationToken) => throw new NotImplementedException();
        public async Task<SnmpWalkResponse> WalkAsync(string ipAddress, string community, string rootOid, int timeoutMilliseconds, CancellationToken cancellationToken) => new(rootOid, 0, await walk(ipAddress, rootOid, cancellationToken));
        public Task<SnmpSystemInfoResponse> GetSystemInfoAsync(string ipAddress, string community, int timeoutMilliseconds, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<SnmpInterfaceResponse>> GetInterfacesAsync(string ipAddress, string community, int timeoutMilliseconds, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    private static class InterlockedExtensions
    {
        public static void Max(ref int location, int value)
        {
            int current;
            do
            {
                current = location;
                if (current >= value) return;
            } while (Interlocked.CompareExchange(ref location, value, current) != current);
        }
    }
}
