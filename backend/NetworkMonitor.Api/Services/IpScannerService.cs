using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetworkMonitor.Api.Configuration;
using NetworkMonitor.Api.Data;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Services;

public sealed class IpScannerService(
    NetworkMonitorDbContext dbContext,
    IPingService pingService,
    IHostNameResolver hostNameResolver,
    IOptions<IpScannerOptions> options) : IIpScannerService
{
    private readonly IpScannerOptions _options = options.Value;

    public async Task<IpScanResponse> ScanAsync(
        string cidr,
        CancellationToken cancellationToken)
    {
        if (!Ipv4CidrRange.TryParse(cidr, out var range, out var error) || range is null)
        {
            throw new IpScanValidationException(error ?? "CIDR is invalid.");
        }

        if (range.HostCount == 0)
        {
            throw new IpScanValidationException("The CIDR range does not contain any scannable hosts.");
        }

        if (range.HostCount > (ulong)_options.MaxAddressesPerScan)
        {
            throw new IpScanValidationException(
                $"The CIDR range contains {range.HostCount} scannable hosts. "
                + $"The maximum is {_options.MaxAddressesPerScan}.");
        }

        var monitoredDevices = (await dbContext.Devices
                .AsNoTracking()
                .Select(device => new { device.Id, device.IpAddress })
                .ToListAsync(cancellationToken))
            .GroupBy(device => device.IpAddress, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.Ordinal);

        var reachableHosts = new ConcurrentBag<IpScanHostResponse>();
        var stopwatch = Stopwatch.StartNew();
        var parallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = _options.MaxConcurrentPings
        };

        await Parallel.ForEachAsync(
            range.EnumerateHostAddresses(),
            parallelOptions,
            async (ipAddress, scanCancellationToken) =>
            {
                var pingResult = await pingService.CheckAsync(
                    ipAddress,
                    _options.PingTimeoutMilliseconds,
                    scanCancellationToken);

                if (!pingResult.Success)
                {
                    return;
                }

                var hostName = await ResolveHostNameAsync(ipAddress, scanCancellationToken);
                var isMonitored = monitoredDevices.TryGetValue(ipAddress, out var deviceId);
                reachableHosts.Add(new IpScanHostResponse(
                    ipAddress,
                    true,
                    pingResult.RoundtripTimeMs,
                    hostName,
                    isMonitored,
                    isMonitored ? deviceId : null));
            });

        stopwatch.Stop();
        var orderedResults = reachableHosts
            .OrderBy(host => Ipv4CidrRange.ToUInt32(IPAddress.Parse(host.IpAddress)))
            .ToList();

        return new IpScanResponse(
            range.CanonicalCidr,
            checked((int)range.HostCount),
            orderedResults.Count,
            stopwatch.ElapsedMilliseconds,
            orderedResults);
    }

    private async Task<string?> ResolveHostNameAsync(
        string ipAddress,
        CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromMilliseconds(_options.HostNameTimeoutMilliseconds);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            return await hostNameResolver
                .ResolveAsync(IPAddress.Parse(ipAddress), timeoutSource.Token)
                .WaitAsync(timeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }
}
