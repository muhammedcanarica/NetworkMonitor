using System.Net;

namespace NetworkMonitor.Api.Services;

public interface IWakeOnLanPacketSender
{
    Task SendAsync(
        byte[] packet,
        IPAddress broadcastAddress,
        int port,
        CancellationToken cancellationToken);
}
