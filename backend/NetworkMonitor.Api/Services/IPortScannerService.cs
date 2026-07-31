using NetworkMonitor.Api.Dtos;

namespace NetworkMonitor.Api.Services;

public interface IPortScannerService
{
    Task<PortScanResponse> ScanAsync(
        PortScanRequest request,
        CancellationToken cancellationToken);
}
