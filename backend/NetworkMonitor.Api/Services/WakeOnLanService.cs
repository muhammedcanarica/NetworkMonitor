using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using NetworkMonitor.Api.Dtos;

namespace NetworkMonitor.Api.Services;

public sealed partial class WakeOnLanService(IWakeOnLanPacketSender packetSender) : IWakeOnLanService
{
    public async Task<WakeOnLanResponse> SendAsync(
        WakeOnLanRequest request,
        CancellationToken cancellationToken)
    {
        var macAddress = ParseMacAddress(request.MacAddress);
        var broadcastAddress = ParseBroadcastAddress(request.BroadcastAddress);
        ValidatePort(request.Port);
        var packet = BuildMagicPacket(macAddress);

        try
        {
            await packetSender.SendAsync(packet, broadcastAddress, request.Port, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SocketException exception)
        {
            throw new WakeOnLanOperationException(
                "The magic packet could not be sent. Check the broadcast address and network connection.",
                exception);
        }
        catch (Exception exception)
        {
            throw new WakeOnLanOperationException(
                "The magic packet could not be sent due to a network error.",
                exception);
        }

        return new WakeOnLanResponse(
            FormatMacAddress(macAddress),
            broadcastAddress.ToString(),
            request.Port,
            "Magic packet sent. The target device may take a moment to wake if it supports Wake-on-LAN.");
    }

    public static byte[] BuildMagicPacket(byte[] macAddress)
    {
        ArgumentNullException.ThrowIfNull(macAddress);
        if (macAddress.Length != 6)
        {
            throw new ArgumentException("A MAC address must contain exactly six bytes.", nameof(macAddress));
        }

        var packet = new byte[102];
        Array.Fill(packet, (byte)0xFF, 0, 6);
        for (var index = 6; index < packet.Length; index += macAddress.Length)
        {
            Buffer.BlockCopy(macAddress, 0, packet, index, macAddress.Length);
        }

        return packet;
    }

    private static byte[] ParseMacAddress(string macAddress)
    {
        var normalized = macAddress?.Trim() ?? string.Empty;
        if (!MacAddressPattern().IsMatch(normalized))
        {
            throw new WakeOnLanValidationException(
                "MAC address must contain six hexadecimal byte pairs, such as 00:11:22:33:44:55.");
        }

        var hex = normalized.Replace(":", string.Empty).Replace("-", string.Empty);
        return Convert.FromHexString(hex);
    }

    private static IPAddress ParseBroadcastAddress(string broadcastAddress)
    {
        if (!IPAddress.TryParse(broadcastAddress?.Trim(), out var address)
            || address.AddressFamily != AddressFamily.InterNetwork
            || address.Equals(IPAddress.Any))
        {
            throw new WakeOnLanValidationException(
                "Broadcast address must be a valid IPv4 broadcast address.");
        }

        return address;
    }

    private static void ValidatePort(int port)
    {
        if (port is < 1 or > 65535)
        {
            throw new WakeOnLanValidationException("UDP port must be between 1 and 65535.");
        }
    }

    private static string FormatMacAddress(byte[] macAddress) => string.Join(':', macAddress.Select(value => value.ToString("X2")));

    [GeneratedRegex("^(?:[0-9A-Fa-f]{12}|(?:[0-9A-Fa-f]{2}:){5}[0-9A-Fa-f]{2}|(?:[0-9A-Fa-f]{2}-){5}[0-9A-Fa-f]{2})$")]
    private static partial Regex MacAddressPattern();
}
