using NetworkMonitor.Api.Dtos;

namespace NetworkMonitor.Api.Services;

public interface IWakeOnLanService
{
    Task<WakeOnLanResponse> SendAsync(
        WakeOnLanRequest request,
        CancellationToken cancellationToken);
}
