using NetworkMonitor.Api.Dtos;

namespace NetworkMonitor.Api.Services;

public interface IConfigBackupStorageService
{
    Task<SaveConfigBackupResponse> SaveAsync(
        SaveConfigBackupRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ConfigBackupListItemResponse>> ListAsync(
        int? deviceId,
        CancellationToken cancellationToken);

    Task<ConfigBackupDetailResponse> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<ConfigBackupComparisonResponse> CompareAsync(
        int fromId,
        int toId,
        CancellationToken cancellationToken);
}
