using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Services;

public interface IPingService
{
    Task<PingCheckResult> CheckAsync(
        string ipAddress,
        int timeoutMilliseconds,
        CancellationToken cancellationToken);
}
