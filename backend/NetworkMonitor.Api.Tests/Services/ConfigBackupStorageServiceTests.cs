using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using NetworkMonitor.Api.Configuration;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Models;
using NetworkMonitor.Api.Services;
using NetworkMonitor.Api.Tests.Infrastructure;

namespace NetworkMonitor.Api.Tests.Services;

public sealed class ConfigBackupStorageServiceTests
{
    [Fact]
    public async Task SaveAsync_CreatesBackupWithDeterministicNormalizedHash()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);

        var result = await service.SaveAsync(CreateRequest("hostname core\r\nend\r\n"), CancellationToken.None);

        Assert.True(result.ConfigurationChanged);
        Assert.Null(result.ExistingBackupId);
        Assert.Equal(1, await database.Context.ConfigBackups.CountAsync());
        Assert.Equal(
            ConfigBackupStorageService.ComputeHash("hostname core\nend\n"),
            result.Backup.Hash);
    }

    [Fact]
    public async Task SaveAsync_DetectsDuplicateConfigurationAfterLineEndingNormalization()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);
        var first = await service.SaveAsync(CreateRequest("hostname core\r\nend\r\n"), CancellationToken.None);

        var duplicate = await service.SaveAsync(CreateRequest("hostname core\nend\n"), CancellationToken.None);

        Assert.False(duplicate.ConfigurationChanged);
        Assert.Equal(first.BackupId, duplicate.ExistingBackupId);
        Assert.Equal(1, await database.Context.ConfigBackups.CountAsync());
    }

    [Fact]
    public async Task SaveAsync_CreatesNewBackupWhenConfigurationChanges()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);
        await service.SaveAsync(CreateRequest("hostname core\n"), CancellationToken.None);

        var changed = await service.SaveAsync(CreateRequest("hostname edge\n"), CancellationToken.None);

        Assert.True(changed.ConfigurationChanged);
        Assert.Equal(2, await database.Context.ConfigBackups.CountAsync());
    }

    [Fact]
    public async Task SaveAsync_RejectsUnsupportedVendorAndWhitespaceConfiguration()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);

        var unsupportedVendor = new SaveConfigBackupRequest
        {
            IpAddress = "192.168.1.10",
            Vendor = (ConfigBackupVendor)99,
            Configuration = "hostname core"
        };

        await Assert.ThrowsAsync<ConfigBackupStorageValidationException>(() =>
            service.SaveAsync(unsupportedVendor, CancellationToken.None));
        await Assert.ThrowsAsync<ConfigBackupStorageValidationException>(() =>
            service.SaveAsync(CreateRequest("   "), CancellationToken.None));
    }

    [Fact]
    public async Task SaveAsync_SupportsManagedAndUnmanagedDeviceBackups()
    {
        await using var database = await TestDatabase.CreateAsync();
        var device = await database.AddDeviceAsync(ipAddress: "192.168.1.10");
        var service = CreateService(database);

        var managed = await service.SaveAsync(CreateRequest("hostname core\n", device.Id), CancellationToken.None);
        var unmanaged = await service.SaveAsync(CreateRequest("hostname edge\n", null, "192.168.1.20"), CancellationToken.None);

        Assert.Equal(device.Id, managed.Backup.DeviceId);
        Assert.Null(unmanaged.Backup.DeviceId);
    }

    [Fact]
    public async Task ListAndGetById_ReturnMetadataThenConfigurationDetail()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);
        var saved = await service.SaveAsync(CreateRequest("hostname core\n"), CancellationToken.None);

        var list = await service.ListAsync(null, CancellationToken.None);
        var detail = await service.GetByIdAsync(saved.BackupId, CancellationToken.None);

        var item = Assert.Single(list);
        Assert.Equal(saved.BackupId, item.Id);
        Assert.Equal("hostname core\n".Length, item.ConfigurationLength);
        Assert.Equal("hostname core\n", detail.Configuration);
    }

    [Fact]
    public async Task CompareAsync_ReturnsAddedAndRemovedLines()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);
        var from = await service.SaveAsync(CreateRequest("hostname core\ninterface Gi0/1\n"), CancellationToken.None);
        var to = await service.SaveAsync(CreateRequest("hostname core\ninterface Gi0/2\n"), CancellationToken.None);

        var comparison = await service.CompareAsync(from.BackupId, to.BackupId, CancellationToken.None);

        Assert.True(comparison.Changed);
        Assert.Equal(1, comparison.AddedLines);
        Assert.Equal(1, comparison.RemovedLines);
        Assert.Contains(comparison.DiffLines, line => line.Type == ConfigDiffLineType.Removed && line.Content == "interface Gi0/1");
        Assert.Contains(comparison.DiffLines, line => line.Type == ConfigDiffLineType.Added && line.Content == "interface Gi0/2");
    }

    [Fact]
    public async Task CompareAsync_ReportsIdenticalConfigurationsWithoutChanges()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);
        var now = DateTimeOffset.UtcNow;
        database.Context.ConfigBackups.AddRange(
            CreateStoredBackup("hostname core\n", now),
            CreateStoredBackup("hostname core\n", now.AddSeconds(1)));
        await database.Context.SaveChangesAsync();

        var comparison = await service.CompareAsync(1, 2, CancellationToken.None);

        Assert.False(comparison.Changed);
        Assert.Equal(0, comparison.AddedLines);
        Assert.Equal(0, comparison.RemovedLines);
    }

    [Fact]
    public async Task CompareAsync_RejectsSameOrMissingBackupId()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);
        var saved = await service.SaveAsync(CreateRequest("hostname core\n"), CancellationToken.None);

        await Assert.ThrowsAsync<ConfigBackupStorageValidationException>(() =>
            service.CompareAsync(saved.BackupId, saved.BackupId, CancellationToken.None));
        await Assert.ThrowsAsync<ConfigBackupNotFoundException>(() =>
            service.GetByIdAsync(999, CancellationToken.None));
    }

    [Fact]
    public async Task SaveAndCompare_EnforceConfigurationLimitsWithoutLeakingContent()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database, maxStoredConfigurationLength: 10, maxDiffLines: 2);

        var saveException = await Assert.ThrowsAsync<ConfigBackupSizeLimitException>(() =>
            service.SaveAsync(CreateRequest("sensitive configuration content"), CancellationToken.None));
        Assert.DoesNotContain("sensitive configuration content", saveException.Message, StringComparison.Ordinal);

        var now = DateTimeOffset.UtcNow;
        database.Context.ConfigBackups.AddRange(
            CreateStoredBackup("one\ntwo\nthree", now),
            CreateStoredBackup("one\ntwo\nfour", now.AddSeconds(1)));
        await database.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<ConfigBackupSizeLimitException>(() => service.CompareAsync(1, 2, CancellationToken.None));
    }

    [Fact]
    public async Task ListAsync_RespectsCancellation()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ListAsync(null, cancellationSource.Token));
    }

    private static ConfigBackupStorageService CreateService(
        TestDatabase database,
        int maxStoredConfigurationLength = 1_048_576,
        int maxDiffLines = 2_000)
    {
        return new ConfigBackupStorageService(
            database.Context,
            new ConfigDiffService(),
            Options.Create(new ConfigBackupOptions
            {
                MaxStoredConfigurationLength = maxStoredConfigurationLength,
                MaxDiffLines = maxDiffLines
            }));
    }

    private static SaveConfigBackupRequest CreateRequest(
        string configuration,
        int? deviceId = null,
        string ipAddress = "192.168.1.10") => new()
    {
        DeviceId = deviceId,
        IpAddress = ipAddress,
        Vendor = ConfigBackupVendor.CiscoIos,
        Configuration = configuration,
        CapturedAt = DateTimeOffset.UtcNow
    };

    private static ConfigBackup CreateStoredBackup(string configuration, DateTimeOffset createdAt) => new()
    {
        IpAddress = "192.168.1.10",
        Vendor = ConfigBackupVendor.CiscoIos,
        Configuration = configuration,
        CapturedAt = createdAt,
        CreatedAt = createdAt,
        Hash = ConfigBackupStorageService.ComputeHash(configuration)
    };
}
