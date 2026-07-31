using NetworkMonitor.Api.Dtos;

namespace NetworkMonitor.Api.Services;

public interface IConfigBackupService
{
    Task<ConfigBackupResponse> GetRunningConfigurationAsync(
        ConfigBackupRequest request,
        CancellationToken cancellationToken);
}
