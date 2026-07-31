using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using NetworkMonitor.Api.Configuration;
using NetworkMonitor.Api.Dtos;

namespace NetworkMonitor.Api.Services;

public sealed class PortScannerService(
    ITcpPortProbe tcpPortProbe,
    IOptions<PortScannerOptions> options) : IPortScannerService
{
    private static readonly IReadOnlyDictionary<int, string> CommonServices = new Dictionary<int, string>
    {
        [20] = "FTP Data",
        [21] = "FTP",
        [22] = "SSH",
        [23] = "Telnet",
        [25] = "SMTP",
        [53] = "DNS",
        [80] = "HTTP",
        [110] = "POP3",
        [143] = "IMAP",
        [443] = "HTTPS",
        [445] = "SMB",
        [1433] = "MSSQL",
        [3306] = "MySQL",
        [3389] = "RDP",
        [5432] = "PostgreSQL",
        [5900] = "VNC",
        [8080] = "HTTP Alt"
    };

    private readonly PortScannerOptions _options = options.Value;

    public async Task<PortScanResponse> ScanAsync(
        PortScanRequest request,
        CancellationToken cancellationToken)
    {
        var address = ParseIpv4Address(request.IpAddress);
        var ports = NormalizeAndValidatePorts(request.Ports);
        ValidateTimeout(request.TimeoutMilliseconds);

        var results = new ConcurrentBag<PortScanResult>();
        var stopwatch = Stopwatch.StartNew();
        var parallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = _options.MaxConcurrentConnections
        };

        try
        {
            await Parallel.ForEachAsync(ports, parallelOptions, async (port, scanCancellationToken) =>
            {
                var probe = await tcpPortProbe.ProbeAsync(
                    address,
                    port,
                    request.TimeoutMilliseconds,
                    scanCancellationToken);
                results.Add(new PortScanResult(
                    port,
                    probe.IsOpen ? PortState.Open : PortState.Closed,
                    probe.LatencyMs,
                    CommonServices.GetValueOrDefault(port)));
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SocketException exception)
        {
            throw new PortScanOperationException(
                "The TCP port scan could not be completed because of a network error.",
                exception);
        }
        catch (Exception exception)
        {
            throw new PortScanOperationException(
                "The TCP port scan could not be completed unexpectedly.",
                exception);
        }

        stopwatch.Stop();
        var orderedResults = results.OrderBy(result => result.Port).ToList();
        return new PortScanResponse(
            address.ToString(),
            orderedResults.Count,
            orderedResults.Count(result => result.State == PortState.Open),
            stopwatch.ElapsedMilliseconds,
            orderedResults);
    }

    private IPAddress ParseIpv4Address(string ipAddress)
    {
        if (!IPAddress.TryParse(ipAddress?.Trim(), out var address)
            || address.AddressFamily != AddressFamily.InterNetwork)
        {
            throw new PortScanValidationException("IP address must be a valid IPv4 address.");
        }

        return address;
    }

    private IReadOnlyList<int> NormalizeAndValidatePorts(IReadOnlyList<int>? requestedPorts)
    {
        if (requestedPorts is null || requestedPorts.Count == 0)
        {
            throw new PortScanValidationException("At least one TCP port must be provided.");
        }

        if (requestedPorts.Any(port => port is < 1 or > 65535))
        {
            throw new PortScanValidationException("TCP ports must be between 1 and 65535.");
        }

        var ports = requestedPorts.Distinct().OrderBy(port => port).ToList();
        if (ports.Count > _options.MaxPortsPerScan)
        {
            throw new PortScanValidationException(
                $"A maximum of {_options.MaxPortsPerScan} TCP ports can be scanned at once.");
        }

        return ports;
    }

    private void ValidateTimeout(int timeoutMilliseconds)
    {
        if (timeoutMilliseconds < _options.MinimumTimeoutMilliseconds
            || timeoutMilliseconds > _options.MaximumTimeoutMilliseconds)
        {
            throw new PortScanValidationException(
                $"Timeout must be between {_options.MinimumTimeoutMilliseconds} and {_options.MaximumTimeoutMilliseconds} milliseconds.");
        }
    }
}
