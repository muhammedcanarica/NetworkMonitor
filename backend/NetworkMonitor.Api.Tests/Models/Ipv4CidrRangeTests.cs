using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Tests.Models;

public sealed class Ipv4CidrRangeTests
{
    [Fact]
    public void TryParse_NormalizesNetworkAddress()
    {
        var success = Ipv4CidrRange.TryParse(
            "192.168.1.42/24",
            out var range,
            out var error);

        Assert.True(success, error);
        Assert.NotNull(range);
        Assert.Equal("192.168.1.0/24", range.CanonicalCidr);
    }

    [Fact]
    public void EnumerateHostAddresses_ForSlash24ExcludesNetworkAndBroadcast()
    {
        Assert.True(Ipv4CidrRange.TryParse("192.168.1.0/24", out var range, out _));

        var addresses = Assert.IsAssignableFrom<IReadOnlyList<string>>(
            range!.EnumerateHostAddresses().ToList());

        Assert.Equal(254, addresses.Count);
        Assert.Equal("192.168.1.1", addresses[0]);
        Assert.Equal("192.168.1.254", addresses[^1]);
    }

    [Fact]
    public void EnumerateHostAddresses_ForSlash30ReturnsTwoHosts()
    {
        Assert.True(Ipv4CidrRange.TryParse("127.0.0.0/30", out var range, out _));

        Assert.Equal(new[] { "127.0.0.1", "127.0.0.2" }, range!.EnumerateHostAddresses());
    }

    [Fact]
    public void EnumerateHostAddresses_ForPointToPointSlash31IncludesBothAddresses()
    {
        Assert.True(Ipv4CidrRange.TryParse("192.0.2.10/31", out var range, out _));

        Assert.Equal(new[] { "192.0.2.10", "192.0.2.11" }, range!.EnumerateHostAddresses());
    }

    [Theory]
    [InlineData("")]
    [InlineData("192.168.1.0")]
    [InlineData("192.168.1.0/33")]
    [InlineData("not-an-address/24")]
    public void TryParse_RejectsInvalidCidr(string cidr)
    {
        Assert.False(Ipv4CidrRange.TryParse(cidr, out var range, out var error));
        Assert.Null(range);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryParse_RejectsIpv6()
    {
        Assert.False(Ipv4CidrRange.TryParse("::1/128", out var range, out var error));
        Assert.Null(range);
        Assert.Equal("Only IPv4 CIDR ranges are supported.", error);
    }
}
