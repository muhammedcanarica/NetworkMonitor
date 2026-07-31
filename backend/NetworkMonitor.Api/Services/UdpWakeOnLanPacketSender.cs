using System.Net;
using System.Net.Sockets;

namespace NetworkMonitor.Api.Services;

public sealed class UdpWakeOnLanPacketSender : IWakeOnLanPacketSender
{
    public async Task SendAsync(
        byte[] packet,
        IPAddress broadcastAddress,
        int port,
        CancellationToken cancellationToken)
    {
        using var client = new UdpClient(AddressFamily.InterNetwork);
        client.EnableBroadcast = true;
        await client.SendAsync(packet, new IPEndPoint(broadcastAddress, port), cancellationToken);
    }
}
