using Microsoft.EntityFrameworkCore;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Models;
using NetworkMonitor.Api.Services;
using NetworkMonitor.Api.Tests.Infrastructure;

namespace NetworkMonitor.Api.Tests.Services;

public sealed class SnmpMonitoringConfigurationServiceTests
{
    [Fact]
    public async Task UpdateAsync_CreatesSingleProfileAndTracksOnlySelectedInterfaces()
    {
        await using var database = await TestDatabase.CreateAsync();
        var device = await database.AddDeviceAsync();
        var credential = await AddCredential(database);
        var resolver = new StubNetworkOperationCredentialResolver
        {
            SnmpHandler = (community, id, _) => { Assert.Null(community); Assert.Equal(credential.Id, id); return Task.FromResult("private"); }
        };
        var service = new SnmpMonitoringConfigurationService(database.Context, resolver, new StubSnmpService());

        var result = await service.UpdateAsync(device.Id, new UpdateSnmpMonitoringRequest { CredentialId = credential.Id, InterfaceIndexes = [1] }, CancellationToken.None);

        Assert.True(result.IsEnabled);
        Assert.Equal("eth0", Assert.Single(result.Interfaces).InterfaceName);
        Assert.Single(await database.Context.SnmpMonitoringProfiles.ToListAsync());
        Assert.Single(await database.Context.SnmpMonitoredInterfaces.ToListAsync());
    }

    [Fact]
    public async Task UpdateAsync_RejectsDuplicateOrUnavailableInterfaces()
    {
        await using var database = await TestDatabase.CreateAsync();
        var device = await database.AddDeviceAsync();
        var credential = await AddCredential(database);
        var service = new SnmpMonitoringConfigurationService(database.Context, new StubNetworkOperationCredentialResolver(), new StubSnmpService());

        await Assert.ThrowsAsync<SnmpMonitoringValidationException>(() => service.UpdateAsync(device.Id, new UpdateSnmpMonitoringRequest { CredentialId = credential.Id, InterfaceIndexes = [1, 1] }, CancellationToken.None));
        await Assert.ThrowsAsync<SnmpMonitoringValidationException>(() => service.UpdateAsync(device.Id, new UpdateSnmpMonitoringRequest { CredentialId = credential.Id, InterfaceIndexes = [99] }, CancellationToken.None));
    }

    [Fact]
    public async Task GetHistoryAsync_ValidatesRangeInterfaceAndOrdersSamples()
    {
        await using var database = await TestDatabase.CreateAsync();
        var device = await database.AddDeviceAsync();
        var credential = await AddCredential(database);
        var service = new SnmpMonitoringConfigurationService(database.Context, new StubNetworkOperationCredentialResolver(), new StubSnmpService());
        await service.UpdateAsync(device.Id, new UpdateSnmpMonitoringRequest { CredentialId = credential.Id, InterfaceIndexes = [1] }, CancellationToken.None);
        var monitored = await database.Context.SnmpMonitoredInterfaces.SingleAsync();
        database.Context.InterfaceTrafficSamples.AddRange(
            Sample(monitored.Id, DateTimeOffset.UtcNow.AddMinutes(-2), 1),
            Sample(monitored.Id, DateTimeOffset.UtcNow.AddMinutes(-1), 2));
        await database.Context.SaveChangesAsync();

        var history = await service.GetHistoryAsync(device.Id, 1, 1, CancellationToken.None);

        Assert.Equal(2, history.Samples.Count);
        Assert.True(history.Samples[0].Timestamp < history.Samples[1].Timestamp);
        await Assert.ThrowsAsync<SnmpMonitoringValidationException>(() => service.GetHistoryAsync(device.Id, 1, 2, CancellationToken.None));
        await Assert.ThrowsAsync<SnmpMonitoringNotFoundException>(() => service.GetHistoryAsync(device.Id, 99, 1, CancellationToken.None));
        await Assert.ThrowsAsync<SnmpMonitoringNotFoundException>(() => service.GetAsync(999, CancellationToken.None));
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsAdminOperStatusAndActualOpenDownIncidentState()
    {
        await using var database = await TestDatabase.CreateAsync();
        var device = await database.AddDeviceAsync();
        var credential = await AddCredential(database);
        var service = new SnmpMonitoringConfigurationService(database.Context, new StubNetworkOperationCredentialResolver(), new StubSnmpService());
        await service.UpdateAsync(device.Id, new UpdateSnmpMonitoringRequest { CredentialId = credential.Id, InterfaceIndexes = [1] }, CancellationToken.None);
        var monitored = await database.Context.SnmpMonitoredInterfaces.SingleAsync();
        database.Context.InterfaceTrafficSamples.Add(new InterfaceTrafficSample
        {
            SnmpMonitoredInterfaceId = monitored.Id, Timestamp = DateTimeOffset.UtcNow,
            InOctets = 1, OutOctets = 1, AdminStatus = "Up", OperStatus = "Down"
        });
        await database.Context.SaveChangesAsync();

        var withoutIncident = Assert.Single(await service.GetSummaryAsync(device.Id, CancellationToken.None));
        Assert.Equal("Up", withoutIncident.AdminStatus);
        Assert.Equal("Down", withoutIncident.OperStatus);
        Assert.False(withoutIncident.HasActiveDownIncident);

        database.Context.Incidents.Add(new Incident
        {
            DeviceId = device.Id, SnmpMonitoredInterfaceId = monitored.Id,
            Type = IncidentType.InterfaceDown, Status = IncidentStatus.Open,
            Summary = "Interface eth0 is down", StartedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        });
        await database.Context.SaveChangesAsync();

        Assert.True(Assert.Single(await service.GetSummaryAsync(device.Id, CancellationToken.None)).HasActiveDownIncident);
    }

    private static InterfaceTrafficSample Sample(int interfaceId, DateTimeOffset timestamp, long octets) => new() { SnmpMonitoredInterfaceId = interfaceId, Timestamp = timestamp, InOctets = octets, OutOctets = octets, OperStatus = "Up", SysUpTimeTicks = 1 };
    private static async Task<NetworkCredential> AddCredential(TestDatabase database)
    {
        var credential = new NetworkCredential { Name = "SNMP", Type = NetworkCredentialType.SnmpV2Community, ProtectedSecret = "x", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        database.Context.Add(credential); await database.Context.SaveChangesAsync(); return credential;
    }

    private sealed class StubSnmpService : ISnmpService
    {
        public Task<IReadOnlyList<SnmpInterfaceResponse>> GetInterfacesAsync(string ipAddress, string community, int timeoutMilliseconds, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<SnmpInterfaceResponse>>([
                new SnmpInterfaceResponse(1, "eth0", "Ethernet 0", "Up", "Up", 1_000_000_000),
                new SnmpInterfaceResponse(2, "eth1", "Ethernet 1", "Up", "Down", 1_000_000_000)
            ]);
        public Task<SnmpValueResponse> GetAsync(string ipAddress, string community, string oid, int timeoutMilliseconds, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<SnmpWalkResponse> WalkAsync(string ipAddress, string community, string rootOid, int timeoutMilliseconds, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<SnmpSystemInfoResponse> GetSystemInfoAsync(string ipAddress, string community, int timeoutMilliseconds, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
