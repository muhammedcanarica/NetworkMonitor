using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NetworkMonitor.Api.Configuration;
using NetworkMonitor.Api.Data;
using NetworkMonitor.Api.Models;
using NetworkMonitor.Api.Services;

namespace NetworkMonitor.Api.Tests.Services;

public sealed class SnmpBandwidthMonitoringServiceTests
{
    [Fact]
    public async Task RunCycleAsync_UsesBoundedConcurrencySkipsDisabledContinuesAfterFailureAndCleansHistory()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var poller = new TrackingPoller(failFirst: true);
        var services = new ServiceCollection();
        services.AddDbContext<NetworkMonitorDbContext>(options => options.UseSqlite(connection));
        services.AddSingleton<ISnmpBandwidthProfilePoller>(poller);
        await using var provider = services.BuildServiceProvider();
        int disabledProfileId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NetworkMonitorDbContext>();
            await db.Database.EnsureCreatedAsync();
            var credential = new NetworkCredential { Name = "SNMP", Type = NetworkCredentialType.SnmpV2Community, ProtectedSecret = "x", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
            db.Add(credential);
            for (var index = 1; index <= 5; index++)
            {
                var device = new Device { Name = $"d{index}", IpAddress = $"192.0.2.{index}", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
                var profile = new SnmpMonitoringProfile { Device = device, Credential = credential, IsEnabled = index != 5, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, Interfaces = [new SnmpMonitoredInterface { InterfaceIndex = 1, InterfaceName = "eth0", IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow }] };
                db.Add(profile);
                if (index == 5) disabledProfileId = profile.Id;
            }
            await db.SaveChangesAsync();
            disabledProfileId = await db.SnmpMonitoringProfiles.Where(item => !item.IsEnabled).Select(item => item.Id).SingleAsync();
            var interfaceId = await db.SnmpMonitoredInterfaces.Select(item => item.Id).FirstAsync();
            db.InterfaceTrafficSamples.Add(new InterfaceTrafficSample { SnmpMonitoredInterfaceId = interfaceId, Timestamp = DateTimeOffset.UtcNow.AddDays(-10), InOctets = 1, OutOctets = 1, OperStatus = "Up", SysUpTimeTicks = 1 });
            await db.SaveChangesAsync();
        }
        var options = Options.Create(new SnmpBandwidthMonitoringOptions { IntervalSeconds = 60, MaxConcurrentDevices = 2, HistoryRetentionDays = 7, RequestTimeoutMilliseconds = 2000 });
        var service = new SnmpBandwidthMonitoringService(provider.GetRequiredService<IServiceScopeFactory>(), options, NullLogger<SnmpBandwidthMonitoringService>.Instance);

        await service.RunCycleAsync(CancellationToken.None);

        Assert.Equal(4, poller.Calls.Count);
        Assert.DoesNotContain(disabledProfileId, poller.Calls);
        Assert.InRange(poller.MaximumConcurrency, 1, 2);
        await using var verifyScope = provider.CreateAsyncScope();
        Assert.Empty(await verifyScope.ServiceProvider.GetRequiredService<NetworkMonitorDbContext>().InterfaceTrafficSamples.ToListAsync());
    }

    [Fact]
    public async Task RunCycleAsync_RespectsCancellation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var poller = new TrackingPoller(false, waitForCancellation: true);
        var services = new ServiceCollection(); services.AddDbContext<NetworkMonitorDbContext>(options => options.UseSqlite(connection)); services.AddSingleton<ISnmpBandwidthProfilePoller>(poller);
        await using var provider = services.BuildServiceProvider();
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NetworkMonitorDbContext>(); await db.Database.EnsureCreatedAsync();
            var credential = new NetworkCredential { Name = "SNMP", Type = NetworkCredentialType.SnmpV2Community, ProtectedSecret = "x", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
            db.Add(new SnmpMonitoringProfile { Device = new Device { Name = "d", IpAddress = "192.0.2.1", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow }, Credential = credential, IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, Interfaces = [new SnmpMonitoredInterface { InterfaceIndex = 1, InterfaceName = "eth0", IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow }] });
            await db.SaveChangesAsync();
        }
        var service = new SnmpBandwidthMonitoringService(provider.GetRequiredService<IServiceScopeFactory>(), Options.Create(new SnmpBandwidthMonitoringOptions { IntervalSeconds = 60, MaxConcurrentDevices = 1, HistoryRetentionDays = 7, RequestTimeoutMilliseconds = 2000 }), NullLogger<SnmpBandwidthMonitoringService>.Instance);
        using var cancellation = new CancellationTokenSource(100);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.RunCycleAsync(cancellation.Token));
    }

    private sealed class TrackingPoller(bool failFirst, bool waitForCancellation = false) : ISnmpBandwidthProfilePoller
    {
        private int _active;
        private int _failed;
        public List<int> Calls { get; } = [];
        public int MaximumConcurrency { get; private set; }
        public async Task PollAsync(int profileId, CancellationToken cancellationToken)
        {
            lock (Calls) Calls.Add(profileId);
            var active = Interlocked.Increment(ref _active);
            MaximumConcurrency = Math.Max(MaximumConcurrency, active);
            try
            {
                if (failFirst && Interlocked.Exchange(ref _failed, 1) == 0) throw new SnmpOperationException(SnmpErrorKind.Timeout, "timeout");
                if (waitForCancellation) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                else await Task.Delay(30, cancellationToken);
            }
            finally { Interlocked.Decrement(ref _active); }
        }
    }
}
