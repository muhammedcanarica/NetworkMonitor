using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using NetworkMonitor.Api.Configuration;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Services;

namespace NetworkMonitor.Api.Tests.Services;

public sealed class PortScannerServiceTests
{
    [Fact]
    public async Task ScanAsync_NormalizesDuplicatePortsAndMapsKnownServices()
    {
        var service = CreateService(new FakeTcpPortProbe((_, port, _, _) => Task.FromResult(
            new TcpPortProbeResult(port == 22, port == 22 ? 3 : null))));

        var result = await service.ScanAsync(new PortScanRequest
        {
            IpAddress = "127.0.0.1",
            Ports = [443, 22, 443, 81],
            TimeoutMilliseconds = 1000
        }, CancellationToken.None);

        Assert.Equal(3, result.ScannedPorts);
        Assert.Equal(1, result.OpenPorts);
        Assert.Equal([22, 81, 443], result.Results.Select(item => item.Port));
        Assert.Equal(PortState.Open, result.Results[0].State);
        Assert.Equal(3, result.Results[0].LatencyMs);
        Assert.Equal("SSH", result.Results[0].ServiceName);
        Assert.Equal(PortState.Closed, result.Results[1].State);
        Assert.Null(result.Results[1].ServiceName);
        Assert.Equal("HTTPS", result.Results[2].ServiceName);
    }

    [Fact]
    public async Task ScanAsync_MapsAllBuiltInCommonServices()
    {
        var expectedServices = new Dictionary<int, string?>
        {
            [20] = "FTP Data", [21] = "FTP", [22] = "SSH", [23] = "Telnet", [25] = "SMTP",
            [53] = "DNS", [80] = "HTTP", [110] = "POP3", [143] = "IMAP", [443] = "HTTPS",
            [445] = "SMB", [1433] = "MSSQL", [3306] = "MySQL", [3389] = "RDP",
            [5432] = "PostgreSQL", [5900] = "VNC", [8080] = "HTTP Alt"
        };
        var service = CreateService(new FakeTcpPortProbe((_, _, _, _) =>
            Task.FromResult(new TcpPortProbeResult(false, null))));

        var result = await service.ScanAsync(new PortScanRequest
        {
            IpAddress = "127.0.0.1",
            Ports = expectedServices.Keys.ToArray(),
            TimeoutMilliseconds = 1000
        }, CancellationToken.None);

        Assert.Equal(expectedServices, result.Results.ToDictionary(item => item.Port, item => item.ServiceName));
    }

    [Theory]
    [InlineData("not-an-ip", new[] { 80 }, 1000, "IPv4")]
    [InlineData("::1", new[] { 80 }, 1000, "IPv4")]
    [InlineData("127.0.0.1", new int[0], 1000, "At least one")]
    [InlineData("127.0.0.1", new[] { 0 }, 1000, "between 1 and 65535")]
    [InlineData("127.0.0.1", new[] { 65536 }, 1000, "between 1 and 65535")]
    [InlineData("127.0.0.1", new[] { 80 }, 99, "Timeout")]
    [InlineData("127.0.0.1", new[] { 80 }, 10001, "Timeout")]
    public async Task ScanAsync_RejectsInvalidRequests(
        string ipAddress,
        int[] ports,
        int timeoutMilliseconds,
        string expectedMessage)
    {
        var service = CreateService(new FakeTcpPortProbe((_, _, _, _) =>
            Task.FromResult(new TcpPortProbeResult(false, null))));

        var exception = await Assert.ThrowsAsync<PortScanValidationException>(() => service.ScanAsync(
            new PortScanRequest
            {
                IpAddress = ipAddress,
                Ports = ports,
                TimeoutMilliseconds = timeoutMilliseconds
            },
            CancellationToken.None));

        Assert.Contains(expectedMessage, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ScanAsync_RejectsRequestAboveConfiguredPortLimit()
    {
        var service = CreateService(
            new FakeTcpPortProbe((_, _, _, _) => Task.FromResult(new TcpPortProbeResult(false, null))),
            maxPorts: 2);

        var exception = await Assert.ThrowsAsync<PortScanValidationException>(() => service.ScanAsync(
            new PortScanRequest { IpAddress = "127.0.0.1", Ports = [80, 81, 82], TimeoutMilliseconds = 1000 },
            CancellationToken.None));

        Assert.Contains("maximum of 2", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ScanAsync_BoundsConcurrentConnectionAttempts()
    {
        var probe = new ConcurrencyProbe();
        var service = CreateService(probe, maxConcurrency: 3);

        var result = await service.ScanAsync(new PortScanRequest
        {
            IpAddress = "127.0.0.1",
            Ports = Enumerable.Range(1000, 12).ToArray(),
            TimeoutMilliseconds = 1000
        }, CancellationToken.None);

        Assert.Equal(12, result.ScannedPorts);
        Assert.InRange(probe.MaximumConcurrentCalls, 1, 3);
    }

    [Fact]
    public async Task ScanAsync_PropagatesCancellation()
    {
        var service = CreateService(new FakeTcpPortProbe(async (_, _, _, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new TcpPortProbeResult(false, null);
        }));
        using var cancellationSource = new CancellationTokenSource();

        var scanTask = service.ScanAsync(new PortScanRequest
        {
            IpAddress = "127.0.0.1",
            Ports = [80, 443],
            TimeoutMilliseconds = 1000
        }, cancellationSource.Token);
        await cancellationSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scanTask);
    }

    [Fact]
    public async Task ScanAsync_MapsUnexpectedNetworkFailureToOperationException()
    {
        var service = CreateService(new FakeTcpPortProbe((_, _, _, _) =>
            throw new SocketException((int)SocketError.NetworkUnreachable)));

        var exception = await Assert.ThrowsAsync<PortScanOperationException>(() => service.ScanAsync(
            new PortScanRequest { IpAddress = "127.0.0.1", Ports = [80], TimeoutMilliseconds = 1000 },
            CancellationToken.None));

        Assert.Contains("network error", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TcpPortProbe_ReturnsOpenForLocalLoopbackListener()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var result = await new TcpPortProbe().ProbeAsync(
            IPAddress.Loopback,
            port,
            1000,
            CancellationToken.None);

        Assert.True(result.IsOpen);
        Assert.NotNull(result.LatencyMs);
    }

    private static PortScannerService CreateService(
        ITcpPortProbe probe,
        int maxPorts = 256,
        int maxConcurrency = 32)
    {
        return new PortScannerService(
            probe,
            Options.Create(new PortScannerOptions
            {
                MaxPortsPerScan = maxPorts,
                MaxConcurrentConnections = maxConcurrency,
                MinimumTimeoutMilliseconds = 100,
                MaximumTimeoutMilliseconds = 10000
            }));
    }

    private sealed class FakeTcpPortProbe(
        Func<IPAddress, int, int, CancellationToken, Task<TcpPortProbeResult>> probe) : ITcpPortProbe
    {
        public Task<TcpPortProbeResult> ProbeAsync(
            IPAddress address,
            int port,
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            return probe(address, port, timeoutMilliseconds, cancellationToken);
        }
    }

    private sealed class ConcurrencyProbe : ITcpPortProbe
    {
        private int _currentConcurrentCalls;

        public int MaximumConcurrentCalls { get; private set; }

        public async Task<TcpPortProbeResult> ProbeAsync(
            IPAddress address,
            int port,
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            var current = Interlocked.Increment(ref _currentConcurrentCalls);
            MaximumConcurrentCalls = Math.Max(MaximumConcurrentCalls, current);
            try
            {
                await Task.Delay(20, cancellationToken);
                return new TcpPortProbeResult(false, null);
            }
            finally
            {
                Interlocked.Decrement(ref _currentConcurrentCalls);
            }
        }
    }
}
