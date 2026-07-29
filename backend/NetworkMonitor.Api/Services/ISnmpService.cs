using NetworkMonitor.Api.Dtos;

namespace NetworkMonitor.Api.Services;

public interface ISnmpService
{
    Task<SnmpValueResponse> GetAsync(
        string ipAddress,
        string community,
        string oid,
        int timeoutMilliseconds,
        CancellationToken cancellationToken);

    Task<SnmpWalkResponse> WalkAsync(
        string ipAddress,
        string community,
        string rootOid,
        int timeoutMilliseconds,
        CancellationToken cancellationToken);

    Task<SnmpSystemInfoResponse> GetSystemInfoAsync(
        string ipAddress,
        string community,
        int timeoutMilliseconds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SnmpInterfaceResponse>> GetInterfacesAsync(
        string ipAddress,
        string community,
        int timeoutMilliseconds,
        CancellationToken cancellationToken);
}
