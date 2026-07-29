using System.Net;
using System.Net.NetworkInformation;
using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Services;

public sealed class PingService : IPingService
{
    public async Task<PingCheckResult> CheckAsync(
        string ipAddress,
        int timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        if (!IPAddress.TryParse(ipAddress, out var address))
        {
            return PingCheckResult.Failed("The device IP address is invalid.");
        }

        if (timeoutMilliseconds <= 0)
        {
            return PingCheckResult.Failed("The ping timeout must be greater than zero.");
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
                : PingCheckResult.Failed(reply.Status.ToString());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return PingCheckResult.Failed(exception.Message);
        }
    }
}
