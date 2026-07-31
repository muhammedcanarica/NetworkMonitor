using Microsoft.EntityFrameworkCore;
using NetworkMonitor.Api.Models;
using NetworkMonitor.Api.Tests.Infrastructure;

namespace NetworkMonitor.Api.Tests.Models;

public sealed class SnmpMonitoringPersistenceTests
{
    [Fact]
    public async Task UniqueProfileAndInterfaceIndexesAreEnforced()
    {
        await using var database = await TestDatabase.CreateAsync();
        var device = await database.AddDeviceAsync();
        var credential = await AddCredential(database);
        var first = CreateProfile(device.Id, credential.Id, 1);
        database.Context.Add(first);
        await database.Context.SaveChangesAsync();

        database.Context.Add(CreateProfile(device.Id, credential.Id, 2));
        await Assert.ThrowsAsync<DbUpdateException>(() => database.Context.SaveChangesAsync());
        database.Context.ChangeTracker.Clear();

        var profile = await database.Context.SnmpMonitoringProfiles.Include(item => item.Interfaces).SingleAsync();
        profile.Interfaces.Add(new SnmpMonitoredInterface { InterfaceIndex = 1, InterfaceName = "duplicate", IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow });
        await Assert.ThrowsAsync<DbUpdateException>(() => database.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task SamplesPersistInTimeOrderAndDeviceDeleteCascadesEverything()
    {
        await using var database = await TestDatabase.CreateAsync();
        var device = await database.AddDeviceAsync();
        var credential = await AddCredential(database);
        var profile = CreateProfile(device.Id, credential.Id, 7);
        database.Context.Add(profile);
        await database.Context.SaveChangesAsync();
        var monitoredInterface = profile.Interfaces.Single();
        database.Context.InterfaceTrafficSamples.AddRange(
            CreateSample(monitoredInterface.Id, DateTimeOffset.UtcNow.AddMinutes(-1)),
            CreateSample(monitoredInterface.Id, DateTimeOffset.UtcNow));
        await database.Context.SaveChangesAsync();

        var samples = await database.Context.InterfaceTrafficSamples.OrderBy(item => item.Timestamp).ToListAsync();
        Assert.Equal(2, samples.Count);
        Assert.True(samples[0].Timestamp < samples[1].Timestamp);

        database.Context.Devices.Remove(device);
        await database.Context.SaveChangesAsync();
        Assert.Empty(await database.Context.SnmpMonitoringProfiles.ToListAsync());
        Assert.Empty(await database.Context.SnmpMonitoredInterfaces.ToListAsync());
        Assert.Empty(await database.Context.InterfaceTrafficSamples.ToListAsync());
    }

    private static async Task<NetworkCredential> AddCredential(TestDatabase database)
    {
        var credential = new NetworkCredential { Name = "SNMP", Type = NetworkCredentialType.SnmpV2Community, ProtectedSecret = "protected", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        database.Context.Add(credential);
        await database.Context.SaveChangesAsync();
        return credential;
    }

    private static SnmpMonitoringProfile CreateProfile(int deviceId, int credentialId, int interfaceIndex) => new()
    {
        DeviceId = deviceId, CredentialId = credentialId, IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
        Interfaces = [new SnmpMonitoredInterface { InterfaceIndex = interfaceIndex, InterfaceName = $"if{interfaceIndex}", IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow }]
    };

    private static InterfaceTrafficSample CreateSample(int interfaceId, DateTimeOffset timestamp) => new() { SnmpMonitoredInterfaceId = interfaceId, Timestamp = timestamp, InOctets = 1, OutOctets = 2, OperStatus = "Up", SysUpTimeTicks = 100 };
}
