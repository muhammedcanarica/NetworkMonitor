using System.Net;
using System.Net.Sockets;

namespace NetworkMonitor.Api.Models;

public sealed class Ipv4CidrRange
{
    private Ipv4CidrRange(uint networkAddress, int prefixLength)
    {
        NetworkAddress = networkAddress;
        PrefixLength = prefixLength;

        var totalAddresses = 1UL << (32 - prefixLength);
        HostCount = prefixLength <= 30 ? totalAddresses - 2 : totalAddresses;
        FirstHostAddress = prefixLength <= 30 ? networkAddress + 1 : networkAddress;
        CanonicalCidr = $"{ToIpAddress(networkAddress)}/{prefixLength}";
    }

    public string CanonicalCidr { get; }

    public int PrefixLength { get; }

    public ulong HostCount { get; }

    private uint NetworkAddress { get; }

    private uint FirstHostAddress { get; }

    public static bool TryParse(string? cidr, out Ipv4CidrRange? range, out string? error)
    {
        range = null;
        error = null;

        if (string.IsNullOrWhiteSpace(cidr))
        {
            error = "CIDR is required.";
            return false;
        }

        var parts = cidr.Trim().Split('/');
        if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var address))
        {
            error = "CIDR must be a valid IPv4 range such as 192.168.1.0/24.";
            return false;
        }

        if (address.AddressFamily != AddressFamily.InterNetwork)
        {
            error = "Only IPv4 CIDR ranges are supported.";
            return false;
        }

        if (!int.TryParse(parts[1], out var prefixLength) || prefixLength is < 0 or > 32)
        {
            error = "CIDR must be a valid IPv4 range such as 192.168.1.0/24.";
            return false;
        }

        var addressValue = ToUInt32(address);
        var mask = prefixLength == 0 ? 0U : uint.MaxValue << (32 - prefixLength);
        range = new Ipv4CidrRange(addressValue & mask, prefixLength);
        return true;
    }

    public IEnumerable<string> EnumerateHostAddresses()
    {
        for (ulong offset = 0; offset < HostCount; offset++)
        {
            yield return ToIpAddress(FirstHostAddress + (uint)offset).ToString();
        }
    }

    public static uint ToUInt32(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return ((uint)bytes[0] << 24)
            | ((uint)bytes[1] << 16)
            | ((uint)bytes[2] << 8)
            | bytes[3];
    }

    private static IPAddress ToIpAddress(uint address)
    {
        return new IPAddress(new byte[]
        {
            (byte)(address >> 24),
            (byte)(address >> 16),
            (byte)(address >> 8),
            (byte)address
        });
    }
}
