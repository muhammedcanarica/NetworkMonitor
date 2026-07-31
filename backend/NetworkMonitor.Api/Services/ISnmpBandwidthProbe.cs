namespace NetworkMonitor.Api.Services;

public interface ISnmpBandwidthProbe
{
    Task<IReadOnlyList<InterfaceCounterReading>> ReadAsync(
        string ipAddress,
        string community,
        IReadOnlyList<int> interfaceIndexes,
        int timeoutMilliseconds,
        CancellationToken cancellationToken);
}

public sealed record InterfaceCounterReading(
    int InterfaceIndex,
    long InOctets,
    long OutOctets,
    string OperStatus,
    long SysUpTimeTicks,
    long? CounterDiscontinuityTicks);
