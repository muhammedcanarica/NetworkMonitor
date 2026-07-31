using System.Net;

namespace NetworkMonitor.Api.Services;

public interface ITcpPortProbe
{
    Task<TcpPortProbeResult> ProbeAsync(
        IPAddress address,
        int port,
        int timeoutMilliseconds,
        CancellationToken cancellationToken);
}

public sealed record TcpPortProbeResult(bool IsOpen, long? LatencyMs);
