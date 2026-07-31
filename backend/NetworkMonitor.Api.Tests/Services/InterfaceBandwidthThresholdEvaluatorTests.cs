using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NetworkMonitor.Api.Models;
using NetworkMonitor.Api.Services;
using NetworkMonitor.Api.Tests.Infrastructure;

namespace NetworkMonitor.Api.Tests.Services;

public sealed class InterfaceBandwidthThresholdEvaluatorTests
{
    [Fact]
    public async Task EvaluateAsync_RequiresConsecutiveBreachesAndPersistsStateAcrossEvaluatorRestart()
    {
        await using var database = await TestDatabase.CreateAsync();
        var monitored = await AddMonitoredInterface(database, breachCount: 3, recoveryCount: 2);
        var evaluator = new InterfaceBandwidthThresholdEvaluator(database.Context, CreatePublisher(database));

        await evaluator.EvaluateAsync(monitored.Id, Sample(90, 90), CancellationToken.None);
        await evaluator.EvaluateAsync(monitored.Id, Sample(110, null), CancellationToken.None);
        await evaluator.EvaluateAsync(monitored.Id, Sample(null, null), CancellationToken.None);
        Assert.Empty(database.Context.Incidents);
        Assert.Equal(1, (await database.Context.InterfaceBandwidthThresholds.SingleAsync()).InboundConsecutiveBreaches);

        await evaluator.EvaluateAsync(monitored.Id, Sample(80, null), CancellationToken.None);
        Assert.Equal(0, (await database.Context.InterfaceBandwidthThresholds.SingleAsync()).InboundConsecutiveBreaches);
        await evaluator.EvaluateAsync(monitored.Id, Sample(110, null), CancellationToken.None);
        await evaluator.EvaluateAsync(monitored.Id, Sample(120, null), CancellationToken.None);
        database.Context.ChangeTracker.Clear();
        await new InterfaceBandwidthThresholdEvaluator(database.Context, new StubIncidentNotificationPublisher()).EvaluateAsync(monitored.Id, Sample(130, null), CancellationToken.None);

        Assert.Single(await database.Context.Incidents.Where(item => item.Status == IncidentStatus.Open).ToListAsync());
    }

    [Fact]
    public async Task EvaluateAsync_RecoversOnlyAfterRequiredSamplesAndCanOpenANewIncidentLater()
    {
        await using var database = await TestDatabase.CreateAsync();
        var monitored = await AddMonitoredInterface(database, breachCount: 1, recoveryCount: 2);
        var evaluator = new InterfaceBandwidthThresholdEvaluator(database.Context, new StubIncidentNotificationPublisher());
        await evaluator.EvaluateAsync(monitored.Id, Sample(120, null), CancellationToken.None);
        await evaluator.EvaluateAsync(monitored.Id, Sample(90, null), CancellationToken.None);
        Assert.Single(await database.Context.Incidents.Where(item => item.Status == IncidentStatus.Open).ToListAsync());

        await evaluator.EvaluateAsync(monitored.Id, Sample(80, null), CancellationToken.None);
        Assert.Single(await database.Context.Incidents.Where(item => item.Status == IncidentStatus.Resolved).ToListAsync());

        await evaluator.EvaluateAsync(monitored.Id, Sample(140, null), CancellationToken.None);
        Assert.Equal(2, await database.Context.Incidents.CountAsync());
        Assert.Single(await database.Context.Incidents.Where(item => item.Status == IncidentStatus.Open).ToListAsync());
    }

    [Fact]
    public async Task EvaluateAsync_HandlesInboundAndOutboundIndependentlyWithoutDuplicates()
    {
        await using var database = await TestDatabase.CreateAsync();
        var monitored = await AddMonitoredInterface(database, breachCount: 1, recoveryCount: 2);
        var evaluator = new InterfaceBandwidthThresholdEvaluator(database.Context, CreatePublisher(database));

        await evaluator.EvaluateAsync(monitored.Id, Sample(120, 130), CancellationToken.None);
        await evaluator.EvaluateAsync(monitored.Id, Sample(140, 150), CancellationToken.None);

        var open = await database.Context.Incidents.Where(item => item.Status == IncidentStatus.Open).ToListAsync();
        Assert.Equal(2, open.Count);
        Assert.Contains(open, item => item.Type == IncidentType.InterfaceInboundBandwidthHigh);
        Assert.Contains(open, item => item.Type == IncidentType.InterfaceOutboundBandwidthHigh);
        Assert.Equal(2, await database.Context.Notifications.CountAsync());
    }

    [Fact]
    public async Task EvaluateAsync_SkipsDisabledMonitoring()
    {
        await using var database = await TestDatabase.CreateAsync();
        var monitored = await AddMonitoredInterface(database, breachCount: 1, recoveryCount: 1);
        monitored.Profile.IsEnabled = false;
        await database.Context.SaveChangesAsync();

        await new InterfaceBandwidthThresholdEvaluator(database.Context, new StubIncidentNotificationPublisher()).EvaluateAsync(monitored.Id, Sample(500, 500), CancellationToken.None);

        Assert.Empty(database.Context.Incidents);
    }

    private static InterfaceTrafficSample Sample(double? inboundMbps, double? outboundMbps) => new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        InBitsPerSecond = inboundMbps * 1_000_000,
        OutBitsPerSecond = outboundMbps * 1_000_000,
        OperStatus = "Up"
    };

    private static IncidentNotificationPublisher CreatePublisher(TestDatabase database)
        => new(new NotificationService(database.Context), NullLogger<IncidentNotificationPublisher>.Instance);

    private static async Task<SnmpMonitoredInterface> AddMonitoredInterface(TestDatabase database, int breachCount, int recoveryCount)
    {
        var device = await database.AddDeviceAsync();
        var credential = new NetworkCredential { Name = "SNMP", Type = NetworkCredentialType.SnmpV2Community, ProtectedSecret = "x", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var monitored = new SnmpMonitoredInterface
        {
            InterfaceIndex = 1, InterfaceName = "Gi0/1", IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow,
            BandwidthThreshold = new InterfaceBandwidthThreshold { InboundThresholdBitsPerSecond = 100_000_000, OutboundThresholdBitsPerSecond = 100_000_000, BreachSampleCount = breachCount, RecoverySampleCount = recoveryCount, IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            Profile = new SnmpMonitoringProfile { DeviceId = device.Id, Credential = credential, IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow }
        };
        database.Context.Add(monitored);
        await database.Context.SaveChangesAsync();
        return monitored;
    }
}
