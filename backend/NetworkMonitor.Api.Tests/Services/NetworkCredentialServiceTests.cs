using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Models;
using NetworkMonitor.Api.Services;
using NetworkMonitor.Api.Tests.Infrastructure;

namespace NetworkMonitor.Api.Tests.Services;

public sealed class NetworkCredentialServiceTests
{
    [Fact]
    public void DataProtectionProtector_EncryptsAndDecryptsWithoutPlaintextStorage()
    {
        var service = new DataProtectionSecretProtector(DataProtectionProvider.Create("NetworkMonitor.Tests"));
        var encrypted = service.Protect("private-community");
        Assert.NotEqual("private-community", encrypted);
        Assert.Equal("private-community", service.Unprotect(encrypted));
    }

    [Fact]
    public async Task CreateListResolveUpdateAndDelete_KeepSecretOutOfMetadata()
    {
        await using var database = await TestDatabase.CreateAsync();
        var protector = new TestProtector();
        var service = new NetworkCredentialService(database.Context, protector);
        var created = await service.CreateAsync(new CreateNetworkCredentialRequest { Name = "Core SSH", Type = NetworkCredentialType.SshPassword, Username = "operator", Secret = "initial-secret" }, CancellationToken.None);

        var entity = Assert.Single(await database.Context.NetworkCredentials.ToListAsync());
        Assert.NotEqual("initial-secret", entity.ProtectedSecret);
        Assert.DoesNotContain("initial-secret", entity.ToString(), StringComparison.Ordinal);
        Assert.True(created.HasSecret);
        Assert.DoesNotContain("initial-secret", System.Text.Json.JsonSerializer.Serialize(created), StringComparison.Ordinal);
        Assert.Single(await service.ListAsync(CancellationToken.None));
        Assert.Equal("initial-secret", (await service.ResolveAsync(created.Id, CancellationToken.None)).Secret);

        await service.UpdateAsync(created.Id, new UpdateNetworkCredentialRequest { Name = "Core SSH", Type = NetworkCredentialType.SshPassword, Username = "operator", Secret = "replacement" }, CancellationToken.None);
        Assert.Equal("replacement", (await service.ResolveAsync(created.Id, CancellationToken.None)).Secret);
        await service.UpdateAsync(created.Id, new UpdateNetworkCredentialRequest { Name = "Core SSH", Type = NetworkCredentialType.SshPassword, Username = "operator", Secret = "" }, CancellationToken.None);
        Assert.Equal("replacement", (await service.ResolveAsync(created.Id, CancellationToken.None)).Secret);

        await service.DeleteAsync(created.Id, CancellationToken.None);
        Assert.Empty(await database.Context.NetworkCredentials.ToListAsync());
    }

    [Fact]
    public async Task Create_ValidatesTypeSecretDeviceAndCancellation()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = new NetworkCredentialService(database.Context, new TestProtector());
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(new CreateNetworkCredentialRequest { Name = "Bad", Type = (NetworkCredentialType)99, Secret = "x" }, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(new CreateNetworkCredentialRequest { Name = "Missing", Type = NetworkCredentialType.SnmpV2Community, Secret = "" }, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(new CreateNetworkCredentialRequest { Name = "Device", Type = NetworkCredentialType.SnmpV2Community, Secret = "x", DeviceId = 999 }, CancellationToken.None));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ListAsync(cancellation.Token));
    }

    [Fact]
    public async Task Delete_RejectsCredentialUsedByMonitoringProfile()
    {
        await using var database = await TestDatabase.CreateAsync();
        var device = await database.AddDeviceAsync();
        var service = new NetworkCredentialService(database.Context, new TestProtector());
        var credential = await service.CreateAsync(new CreateNetworkCredentialRequest { Name = "Monitoring SNMP", Type = NetworkCredentialType.SnmpV2Community, Secret = "private" }, CancellationToken.None);
        database.Context.SnmpMonitoringProfiles.Add(new SnmpMonitoringProfile { DeviceId = device.Id, CredentialId = credential.Id, IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        await database.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<NetworkCredentialInUseException>(() => service.DeleteAsync(credential.Id, CancellationToken.None));
        Assert.NotNull(await database.Context.NetworkCredentials.FindAsync(credential.Id));
    }

    private sealed class TestProtector : ISecretProtector
    {
        public string Protect(string secret) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(secret));
        public string Unprotect(string protectedSecret) => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(protectedSecret));
    }
}
