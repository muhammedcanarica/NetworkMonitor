using Microsoft.EntityFrameworkCore;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Models;
using NetworkMonitor.Api.Services;
using NetworkMonitor.Api.Tests.Infrastructure;

namespace NetworkMonitor.Api.Tests.Services;

public sealed class InterfaceBandwidthThresholdServiceTests
{
    [Fact]
    public async Task UpdateGetDelete_NormalizesMbpsAndValidatesConfiguration()
    {
        await using var database = await TestDatabase.CreateAsync();
        var monitored = await AddInterface(database);
        var service = new InterfaceBandwidthThresholdService(database.Context);

        var response = await service.UpdateAsync(monitored.Profile.DeviceId, monitored.InterfaceIndex, new UpdateInterfaceBandwidthThresholdRequest { InboundThresholdMbps = 100, BreachSampleCount = 3, RecoverySampleCount = 2 }, CancellationToken.None);

        Assert.Equal(100, response.InboundThresholdMbps);
        Assert.Null(response.OutboundThresholdMbps);
        Assert.Equal(100_000_000, (await database.Context.InterfaceBandwidthThresholds.SingleAsync()).InboundThresholdBitsPerSecond);
        Assert.NotNull(await service.GetAsync(monitored.Profile.DeviceId, monitored.InterfaceIndex, CancellationToken.None));
        await service.DeleteAsync(monitored.Profile.DeviceId, monitored.InterfaceIndex, CancellationToken.None);
        Assert.Empty(database.Context.InterfaceBandwidthThresholds);

        await Assert.ThrowsAsync<InterfaceBandwidthThresholdValidationException>(() => service.UpdateAsync(monitored.Profile.DeviceId, monitored.InterfaceIndex, new UpdateInterfaceBandwidthThresholdRequest(), CancellationToken.None));
        await Assert.ThrowsAsync<InterfaceBandwidthThresholdValidationException>(() => service.UpdateAsync(monitored.Profile.DeviceId, monitored.InterfaceIndex, new UpdateInterfaceBandwidthThresholdRequest { InboundThresholdMbps = -1 }, CancellationToken.None));
        await Assert.ThrowsAsync<InterfaceBandwidthThresholdValidationException>(() => service.UpdateAsync(monitored.Profile.DeviceId, monitored.InterfaceIndex, new UpdateInterfaceBandwidthThresholdRequest { InboundThresholdMbps = double.PositiveInfinity }, CancellationToken.None));
        await Assert.ThrowsAsync<InterfaceBandwidthThresholdValidationException>(() => service.UpdateAsync(monitored.Profile.DeviceId, monitored.InterfaceIndex, new UpdateInterfaceBandwidthThresholdRequest { InboundThresholdMbps = 1, BreachSampleCount = 101 }, CancellationToken.None));
    }

    [Fact]
    public async Task Delete_RejectsOpenBandwidthIncident()
    {
        await using var database = await TestDatabase.CreateAsync();
        var monitored = await AddInterface(database);
        var service = new InterfaceBandwidthThresholdService(database.Context);
        await service.UpdateAsync(monitored.Profile.DeviceId, monitored.InterfaceIndex, new UpdateInterfaceBandwidthThresholdRequest { OutboundThresholdMbps = 50 }, CancellationToken.None);
        database.Context.Incidents.Add(new Incident { DeviceId = monitored.Profile.DeviceId, SnmpMonitoredInterfaceId = monitored.Id, Type = IncidentType.InterfaceOutboundBandwidthHigh, Status = IncidentStatus.Open, Summary = "alert", StartedAt = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        await database.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<InterfaceBandwidthThresholdConflictException>(() => service.DeleteAsync(monitored.Profile.DeviceId, monitored.InterfaceIndex, CancellationToken.None));
    }

    private static async Task<SnmpMonitoredInterface> AddInterface(TestDatabase database)
    {
        var device = await database.AddDeviceAsync();
        var monitored = new SnmpMonitoredInterface { InterfaceIndex = 7, InterfaceName = "Gi0/7", IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow, Profile = new SnmpMonitoringProfile { DeviceId = device.Id, Credential = new NetworkCredential { Name = "SNMP", Type = NetworkCredentialType.SnmpV2Community, ProtectedSecret = "x", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow }, IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow } };
        database.Context.Add(monitored); await database.Context.SaveChangesAsync(); return monitored;
    }
}
