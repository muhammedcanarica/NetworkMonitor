using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Services;

public interface ISnmpTransport
{
    Task<IReadOnlyList<SnmpVariableValue>> GetAsync(
        SnmpConnection connection,
        IReadOnlyList<string> oids,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SnmpVariableValue>> WalkAsync(
        SnmpConnection connection,
        string rootOid,
        int maxResults,
        CancellationToken cancellationToken);
}
