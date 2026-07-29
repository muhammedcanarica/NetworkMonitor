using System.Net;

namespace NetworkMonitor.Api.Services;

public sealed class HostNameResolver(ILogger<HostNameResolver> logger) : IHostNameResolver
{
    public async Task<string?> ResolveAsync(
        IPAddress address,
        CancellationToken cancellationToken)
    {
        try
        {
            var hostEntry = await Dns.GetHostEntryAsync(address.ToString(), cancellationToken);
            return string.IsNullOrWhiteSpace(hostEntry.HostName) ? null : hostEntry.HostName;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Reverse DNS lookup failed for {IpAddress}.", address);
            return null;
        }
    }
}
