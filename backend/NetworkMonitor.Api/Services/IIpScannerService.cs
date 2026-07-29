using NetworkMonitor.Api.Dtos;

namespace NetworkMonitor.Api.Services;

public interface IIpScannerService
{
    Task<IpScanResponse> ScanAsync(string cidr, CancellationToken cancellationToken);
}
