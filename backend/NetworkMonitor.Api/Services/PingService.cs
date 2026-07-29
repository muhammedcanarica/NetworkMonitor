using System.Net;
using System.Net.NetworkInformation;
using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Services;

public sealed class PingService(ILogger<PingService> logger) : IPingService
{
    public async Task<PingCheckResult> CheckAsync(
        string ipAddress,
        int timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        if (!IPAddress.TryParse(ipAddress, out var address))
        {
            return PingCheckResult.Failed(PingFailureReasons.Unknown);
        }

        if (timeoutMilliseconds <= 0)
        {
            return PingCheckResult.Failed(PingFailureReasons.Unknown);
        }

        using var ping = new Ping();

        try
        {
            var reply = await ping.SendPingAsync(
                address,
                TimeSpan.FromMilliseconds(timeoutMilliseconds),
                Array.Empty<byte>(),
                new PingOptions(),
                cancellationToken);

            return reply.Status == IPStatus.Success
                ? PingCheckResult.Succeeded(reply.RoundtripTime)
                : PingCheckResult.Failed(MapFailureReason(reply.Status));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Ping to {IpAddress} failed with an exception.", ipAddress);
            return PingCheckResult.Failed(PingFailureReasons.Unknown);
        }
    }

    private static string MapFailureReason(IPStatus status)
    {
        return status switch
        {
            IPStatus.TimedOut => PingFailureReasons.Timeout,
            IPStatus.DestinationHostUnreachable => PingFailureReasons.DestinationHostUnreachable,
            IPStatus.DestinationNetworkUnreachable => PingFailureReasons.DestinationNetworkUnreachable,
            IPStatus.DestinationPortUnreachable => PingFailureReasons.DestinationPortUnreachable,
            IPStatus.DestinationProtocolUnreachable => PingFailureReasons.DestinationProtocolUnreachable,
            IPStatus.PacketTooBig => PingFailureReasons.PacketTooBig,
            IPStatus.TtlExpired or IPStatus.TtlReassemblyTimeExceeded => PingFailureReasons.TtlExpired,
            IPStatus.BadDestination => PingFailureReasons.BadDestination,
            _ => PingFailureReasons.Unknown
        };
    }
}
