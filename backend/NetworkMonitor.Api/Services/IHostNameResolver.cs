using System.Net;

namespace NetworkMonitor.Api.Services;

public interface IHostNameResolver
{
    Task<string?> ResolveAsync(IPAddress address, CancellationToken cancellationToken);
}
