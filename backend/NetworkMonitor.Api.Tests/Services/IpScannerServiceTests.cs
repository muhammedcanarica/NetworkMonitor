using System.Net;
using Microsoft.Extensions.Options;
using NetworkMonitor.Api.Configuration;
using NetworkMonitor.Api.Models;
using NetworkMonitor.Api.Services;
using NetworkMonitor.Api.Tests.Infrastructure;

namespace NetworkMonitor.Api.Tests.Services;

public sealed class IpScannerServiceTests
{
    [Fact]
    public async Task ScanAsync_RejectsRangeAboveConfiguredLimit()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = CreateService(database, maxAddresses: 4);

        var exception = await Assert.ThrowsAsync<IpScanValidationException>(
            () => service.ScanAsync("192.0.2.0/29", CancellationToken.None));

        Assert.Contains("maximum is 4", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ScanAsync_PropagatesCancellation()
    {
        await using var database = await TestDatabase.CreateAsync();
        var pingService = new FakePingService(async (_, _, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return PingCheckResult.Failed(PingFailureReasons.Timeout);
        });
        var service = CreateService(database, pingService);
        using var cancellationSource = new CancellationTokenSource();

        var scanTask = service.ScanAsync("127.0.0.1/32", cancellationSource.Token);
        await cancellationSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scanTask);
    }

    [Fact]
    public async Task ScanAsync_MatchesExistingDeviceByIpAddress()
    {
        await using var database = await TestDatabase.CreateAsync();
        var device = await database.AddDeviceAsync("Loopback", "127.0.0.1");
        var pingService = new FakePingService((_, _, _) =>
            Task.FromResult(PingCheckResult.Succeeded(2)));
        var resolver = new FakeHostNameResolver((_, _) => Task.FromResult<string?>("localhost"));
        var service = CreateService(database, pingService, resolver);

        var result = await service.ScanAsync("127.0.0.1/32", CancellationToken.None);

        var host = Assert.Single(result.Results);
        Assert.True(host.IsAlreadyMonitored);
        Assert.Equal(device.Id, host.DeviceId);
        Assert.Equal("localhost", host.HostName);
    }

    [Fact]
    public async Task ScanAsync_ReturnsOnlyReachableHostsAndTotalAttemptedCount()
    {
        await using var database = await TestDatabase.CreateAsync();
        var pingService = new FakePingService((address, _, _) => Task.FromResult(
            address == "127.0.0.1"
                ? PingCheckResult.Succeeded(1)
                : PingCheckResult.Failed(PingFailureReasons.Timeout)));
        var service = CreateService(database, pingService);

        var result = await service.ScanAsync("127.0.0.0/30", CancellationToken.None);

        Assert.Equal(2, result.ScannedAddresses);
        Assert.Equal(1, result.ReachableHosts);
        Assert.Equal("127.0.0.1", Assert.Single(result.Results).IpAddress);
    }

    [Fact]
    public async Task ScanAsync_DoesNotWaitIndefinitelyForReverseDns()
    {
        await using var database = await TestDatabase.CreateAsync();
        var pingService = new FakePingService((_, _, _) =>
            Task.FromResult(PingCheckResult.Succeeded(1)));
        var unresolvedTask = new TaskCompletionSource<string?>();
        var resolver = new FakeHostNameResolver((_, _) => unresolvedTask.Task);
        var service = CreateService(database, pingService, resolver, hostNameTimeout: 20);

        var result = await service.ScanAsync("127.0.0.1/32", CancellationToken.None);

        Assert.Null(Assert.Single(result.Results).HostName);
        Assert.InRange(result.DurationMs, 0, 500);
    }

    private static IpScannerService CreateService(
        TestDatabase database,
        IPingService? pingService = null,
        IHostNameResolver? resolver = null,
        int maxAddresses = 1024,
        int hostNameTimeout = 50)
    {
        return new IpScannerService(
            database.Context,
            pingService ?? new FakePingService((_, _, _) =>
                Task.FromResult(PingCheckResult.Failed(PingFailureReasons.Timeout))),
            resolver ?? new FakeHostNameResolver((_, _) => Task.FromResult<string?>(null)),
            Options.Create(new IpScannerOptions
            {
                PingTimeoutMilliseconds = 50,
                MaxConcurrentPings = 4,
                MaxAddressesPerScan = maxAddresses,
                HostNameTimeoutMilliseconds = hostNameTimeout
            }));
    }

    private sealed class FakePingService(
        Func<string, int, CancellationToken, Task<PingCheckResult>> check) : IPingService
    {
        public Task<PingCheckResult> CheckAsync(
            string ipAddress,
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            return check(ipAddress, timeoutMilliseconds, cancellationToken);
        }
    }

    private sealed class FakeHostNameResolver(
        Func<IPAddress, CancellationToken, Task<string?>> resolve) : IHostNameResolver
    {
        public Task<string?> ResolveAsync(
            IPAddress address,
            CancellationToken cancellationToken)
        {
            return resolve(address, cancellationToken);
        }
    }
}
