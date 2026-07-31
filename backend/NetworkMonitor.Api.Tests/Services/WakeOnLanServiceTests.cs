using System.Net;
using System.Net.Sockets;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Services;

namespace NetworkMonitor.Api.Tests.Services;

public sealed class WakeOnLanServiceTests
{
    [Fact]
    public void BuildMagicPacket_UsesWakeOnLanPacketLayout()
    {
        var macAddress = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55 };

        var packet = WakeOnLanService.BuildMagicPacket(macAddress);

        Assert.Equal(102, packet.Length);
        Assert.All(packet[..6], value => Assert.Equal(0xFF, value));
        for (var index = 6; index < packet.Length; index += macAddress.Length)
        {
            Assert.Equal(macAddress, packet[index..(index + macAddress.Length)]);
        }
    }

    [Theory]
    [InlineData("00:11:22:33:44:55", "255.255.255.255", 9)]
    [InlineData("001122334455", "192.168.1.255", 7)]
    public async Task SendAsync_ValidRequestSendsPacket(
        string macAddress,
        string broadcastAddress,
        int port)
    {
        var sender = new RecordingPacketSender();
        var service = new WakeOnLanService(sender);

        var response = await service.SendAsync(
            new WakeOnLanRequest(macAddress, broadcastAddress, port),
            CancellationToken.None);

        Assert.Equal("00:11:22:33:44:55", response.MacAddress);
        Assert.Equal(broadcastAddress, response.BroadcastAddress);
        Assert.Equal(port, response.Port);
        Assert.Contains("Magic packet sent", response.Message);
        Assert.Equal(IPAddress.Parse(broadcastAddress), sender.BroadcastAddress);
        Assert.Equal(port, sender.Port);
        Assert.NotNull(sender.Packet);
        Assert.Equal(102, sender.Packet!.Length);
    }

    [Theory]
    [InlineData("00:11:22:33:44", "255.255.255.255", 9, "MAC")]
    [InlineData("00:11:22:33:44:55", "not-an-ip", 9, "Broadcast")]
    [InlineData("00:11:22:33:44:55", "::1", 9, "Broadcast")]
    [InlineData("00:11:22:33:44:55", "255.255.255.255", 0, "UDP")]
    [InlineData("00:11:22:33:44:55", "255.255.255.255", 65536, "UDP")]
    public async Task SendAsync_InvalidRequestDoesNotSendPacket(
        string macAddress,
        string broadcastAddress,
        int port,
        string expectedMessage)
    {
        var sender = new RecordingPacketSender();
        var service = new WakeOnLanService(sender);

        var exception = await Assert.ThrowsAsync<WakeOnLanValidationException>(() => service.SendAsync(
            new WakeOnLanRequest(macAddress, broadcastAddress, port),
            CancellationToken.None));

        Assert.Contains(expectedMessage, exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(sender.Packet);
    }

    [Fact]
    public async Task SendAsync_ConvertsNetworkFailuresToOperationException()
    {
        var service = new WakeOnLanService(new FailingPacketSender());

        var exception = await Assert.ThrowsAsync<WakeOnLanOperationException>(() => service.SendAsync(
            new WakeOnLanRequest("00:11:22:33:44:55", "255.255.255.255"),
            CancellationToken.None));

        Assert.Contains("could not be sent", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.IsType<SocketException>(exception.InnerException);
    }

    private sealed class RecordingPacketSender : IWakeOnLanPacketSender
    {
        public byte[]? Packet { get; private set; }

        public IPAddress? BroadcastAddress { get; private set; }

        public int? Port { get; private set; }

        public Task SendAsync(
            byte[] packet,
            IPAddress broadcastAddress,
            int port,
            CancellationToken cancellationToken)
        {
            Packet = packet;
            BroadcastAddress = broadcastAddress;
            Port = port;
            return Task.CompletedTask;
        }
    }

    private sealed class FailingPacketSender : IWakeOnLanPacketSender
    {
        public Task SendAsync(
            byte[] packet,
            IPAddress broadcastAddress,
            int port,
            CancellationToken cancellationToken)
        {
            throw new SocketException((int)SocketError.NetworkUnreachable);
        }
    }
}
