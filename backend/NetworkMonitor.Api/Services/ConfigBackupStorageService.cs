using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetworkMonitor.Api.Configuration;
using NetworkMonitor.Api.Data;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Services;

public sealed class ConfigBackupStorageService(
    NetworkMonitorDbContext dbContext,
    IConfigDiffService configDiffService,
    IOptions<ConfigBackupOptions> options) : IConfigBackupStorageService
{
    private readonly ConfigBackupOptions _options = options.Value;

    public async Task<SaveConfigBackupResponse> SaveAsync(
        SaveConfigBackupRequest request,
        CancellationToken cancellationToken)
    {
        var ipAddress = NormalizeIpAddress(request.IpAddress);
        ValidateVendor(request.Vendor);
        ValidateConfiguration(request.Configuration);
        await ValidateDeviceAsync(request.DeviceId, ipAddress, cancellationToken);

        var hash = ComputeHash(request.Configuration);
        var latestBackup = await dbContext.ConfigBackups
            .Where(item => item.IpAddress == ipAddress)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (latestBackup?.Hash == hash)
        {
            var existing = ToListItem(latestBackup);
            return new SaveConfigBackupResponse(false, latestBackup.Id, latestBackup.Id, existing);
        }

        var backup = new ConfigBackup
        {
            DeviceId = request.DeviceId,
            IpAddress = ipAddress,
            Vendor = request.Vendor,
            Configuration = request.Configuration,
            CapturedAt = request.CapturedAt == default ? DateTimeOffset.UtcNow : request.CapturedAt,
            CreatedAt = DateTimeOffset.UtcNow,
            Hash = hash
        };
        dbContext.ConfigBackups.Add(backup);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new SaveConfigBackupResponse(true, backup.Id, null, ToListItem(backup));
    }

    public async Task<IReadOnlyList<ConfigBackupListItemResponse>> ListAsync(
        int? deviceId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.ConfigBackups.AsNoTracking();
        if (deviceId.HasValue)
        {
            query = query.Where(item => item.DeviceId == deviceId.Value);
        }

        return await query
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new ConfigBackupListItemResponse(
                item.Id,
                item.DeviceId,
                item.IpAddress,
                item.Vendor,
                item.CapturedAt,
                item.CreatedAt,
                item.Hash,
                item.Configuration.Length))
            .ToListAsync(cancellationToken);
    }

    public async Task<ConfigBackupDetailResponse> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var backup = await dbContext.ConfigBackups
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new ConfigBackupNotFoundException(id);

        return ToDetail(backup);
    }

    public async Task<ConfigBackupComparisonResponse> CompareAsync(
        int fromId,
        int toId,
        CancellationToken cancellationToken)
    {
        if (fromId == toId)
        {
            throw new ConfigBackupStorageValidationException("Choose two different backups to compare.");
        }

        var backups = await dbContext.ConfigBackups
            .AsNoTracking()
            .Where(item => item.Id == fromId || item.Id == toId)
            .ToListAsync(cancellationToken);
        var fromBackup = backups.SingleOrDefault(item => item.Id == fromId)
            ?? throw new ConfigBackupNotFoundException(fromId);
        var toBackup = backups.SingleOrDefault(item => item.Id == toId)
            ?? throw new ConfigBackupNotFoundException(toId);
        ValidateDiffSize(fromBackup.Configuration, toBackup.Configuration);

        var result = configDiffService.Compare(fromBackup.Configuration, toBackup.Configuration);
        return new ConfigBackupComparisonResponse(
            ToListItem(fromBackup),
            ToListItem(toBackup),
            result.AddedLines,
            result.RemovedLines,
            result.AddedLines > 0 || result.RemovedLines > 0,
            result.Lines);
    }

    public static string ComputeHash(string configuration)
    {
        var normalized = ConfigDiffService.NormalizeLineEndings(configuration);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }

    private async Task ValidateDeviceAsync(int? deviceId, string ipAddress, CancellationToken cancellationToken)
    {
        if (!deviceId.HasValue)
        {
            return;
        }

        var deviceIpAddress = await dbContext.Devices
            .Where(device => device.Id == deviceId.Value)
            .Select(device => device.IpAddress)
            .SingleOrDefaultAsync(cancellationToken);
        if (deviceIpAddress is null)
        {
            throw new ConfigBackupStorageValidationException("The selected device was not found.");
        }

        if (!string.Equals(deviceIpAddress, ipAddress, StringComparison.Ordinal))
        {
            throw new ConfigBackupStorageValidationException("The selected device does not match the backup IP address.");
        }
    }

    private void ValidateConfiguration(string configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration))
        {
            throw new ConfigBackupStorageValidationException("Configuration content is required.");
        }

        if (configuration.Length > _options.MaxStoredConfigurationLength)
        {
            throw new ConfigBackupSizeLimitException(
                $"Configuration content exceeds the {_options.MaxStoredConfigurationLength} character storage limit.");
        }
    }

    private void ValidateDiffSize(string fromConfiguration, string toConfiguration)
    {
        var fromLineCount = CountLines(fromConfiguration);
        var toLineCount = CountLines(toConfiguration);
        if (fromLineCount > _options.MaxDiffLines || toLineCount > _options.MaxDiffLines)
        {
            throw new ConfigBackupSizeLimitException(
                $"Configuration comparison is limited to {_options.MaxDiffLines} lines per backup.");
        }
    }

    private static void ValidateVendor(ConfigBackupVendor vendor)
    {
        if (vendor != ConfigBackupVendor.CiscoIos)
        {
            throw new ConfigBackupStorageValidationException("Only Cisco IOS configuration backups are supported.");
        }
    }

    private static int CountLines(string configuration)
    {
        return ConfigDiffService.NormalizeLineEndings(configuration).Count(character => character == '\n') + 1;
    }

    private static string NormalizeIpAddress(string ipAddress)
    {
        if (!IPAddress.TryParse(ipAddress?.Trim(), out var address)
            || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            throw new ConfigBackupStorageValidationException("IP address must be a valid IPv4 address.");
        }

        return address.ToString();
    }

    private static ConfigBackupListItemResponse ToListItem(ConfigBackup backup)
    {
        return new ConfigBackupListItemResponse(
            backup.Id,
            backup.DeviceId,
            backup.IpAddress,
            backup.Vendor,
            backup.CapturedAt,
            backup.CreatedAt,
            backup.Hash,
            backup.Configuration.Length);
    }

    private static ConfigBackupDetailResponse ToDetail(ConfigBackup backup)
    {
        return new ConfigBackupDetailResponse(
            backup.Id,
            backup.DeviceId,
            backup.IpAddress,
            backup.Vendor,
            backup.Configuration,
            backup.CapturedAt,
            backup.CreatedAt,
            backup.Hash);
    }
}
