using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetworkMonitor.Api.Configuration;
using NetworkMonitor.Api.Models;
using NetworkMonitor.Api.Services;
using NetworkMonitor.Api.Tests.Infrastructure;

namespace NetworkMonitor.Api.Tests.Services;

public sealed class InterfaceStatusIncidentEvaluatorTests
{
    [Fact]
    public async Task EvaluateAsync_UsesBaselineAndPersistentConsecutiveDownSamplesBeforeOpeningIncident()
    {
        await using var database = await TestDatabase.CreateAsync();
        var monitored = await AddMonitoredInterface(database);
        var evaluator = CreateEvaluator(database, downTrigger: 2, upRecovery: 2);

        await evaluator.EvaluateAsync(monitored.Id, "Up", "Down", Timestamp(0), CancellationToken.None);
        await evaluator.EvaluateAsync(monitored.Id, "Up", "Down", Timestamp(1), CancellationToken.None);
        Assert.Empty(database.Context.Incidents);
        Assert.Equal(1, monitored.ConsecutiveDownSamples);

        database.Context.ChangeTracker.Clear();
        await CreateEvaluator(database, downTrigger: 2, upRecovery: 2)
            .EvaluateAsync(monitored.Id, "Up", "Down", Timestamp(2), CancellationToken.None);

        var incident = Assert.Single(await database.Context.Incidents.ToListAsync());
        Assert.Equal(IncidentType.InterfaceDown, incident.Type);
        Assert.Equal(IncidentStatus.Open, incident.Status);
        Assert.Equal(monitored.Id, incident.SnmpMonitoredInterfaceId);
    }

    [Fact]
    public async Task EvaluateAsync_RequiresConsecutiveUpSamplesAndDownResetsRecovery()
    {
        await using var database = await TestDatabase.CreateAsync();
        var monitored = await AddMonitoredInterface(database);
        var evaluator = CreateEvaluator(database, downTrigger: 1, upRecovery: 2);
        await evaluator.EvaluateAsync(monitored.Id, "Up", "Up", Timestamp(0), CancellationToken.None);
        await evaluator.EvaluateAsync(monitored.Id, "Up", "Down", Timestamp(1), CancellationToken.None);

        await evaluator.EvaluateAsync(monitored.Id, "Up", "Up", Timestamp(2), CancellationToken.None);
        await evaluator.EvaluateAsync(monitored.Id, "Up", "Down", Timestamp(3), CancellationToken.None);
        await evaluator.EvaluateAsync(monitored.Id, "Up", "Up", Timestamp(4), CancellationToken.None);
        Assert.Single(await database.Context.Incidents.Where(item => item.Status == IncidentStatus.Open).ToListAsync());

        await evaluator.EvaluateAsync(monitored.Id, "Up", "Up", Timestamp(5), CancellationToken.None);

        var incident = Assert.Single(await database.Context.Incidents.ToListAsync());
        Assert.Equal(IncidentStatus.Resolved, incident.Status);
        Assert.Equal(Timestamp(5), incident.ResolvedAt);
    }

    [Theory]
    [InlineData("Down", InterfaceOperationalState.Problem)]
    [InlineData("LowerLayerDown", InterfaceOperationalState.Problem)]
    [InlineData("Up", InterfaceOperationalState.Up)]
    [InlineData("Testing", InterfaceOperationalState.Neutral)]
    [InlineData("Unknown", InterfaceOperationalState.Neutral)]
    [InlineData("Dormant", InterfaceOperationalState.Neutral)]
    [InlineData("NotPresent", InterfaceOperationalState.Neutral)]
    [InlineData(null, InterfaceOperationalState.Neutral)]
    public void ClassifyOperStatus_MapsSnmpStates(string? status, InterfaceOperationalState expected)
        => Assert.Equal(expected, InterfaceStatusIncidentEvaluator.ClassifyOperStatus(status));

    [Fact]
    public async Task EvaluateAsync_AdminDownAndMissingStatusDoNotOpenOrResolveIncident()
    {
        await using var database = await TestDatabase.CreateAsync();
        var monitored = await AddMonitoredInterface(database);
        var evaluator = CreateEvaluator(database, downTrigger: 1, upRecovery: 1);

        await evaluator.EvaluateAsync(monitored.Id, "Down", "Down", Timestamp(0), CancellationToken.None);
        await evaluator.EvaluateAsync(monitored.Id, null, "Down", Timestamp(1), CancellationToken.None);
        Assert.Empty(database.Context.Incidents);

        await evaluator.EvaluateAsync(monitored.Id, "Up", "Up", Timestamp(2), CancellationToken.None);
        await evaluator.EvaluateAsync(monitored.Id, "Up", "Down", Timestamp(3), CancellationToken.None);
        var open = Assert.Single(await database.Context.Incidents.ToListAsync());

        await evaluator.EvaluateAsync(monitored.Id, "Down", "Down", Timestamp(4), CancellationToken.None);
        await evaluator.EvaluateAsync(monitored.Id, null, "Up", Timestamp(5), CancellationToken.None);

        Assert.Equal(IncidentStatus.Open, (await database.Context.Incidents.FindAsync(open.Id))!.Status);
    }

    [Fact]
    public async Task EvaluateAsync_DoesNotDuplicateAndAllowsBandwidthIncidentForSameInterface()
    {
        await using var database = await TestDatabase.CreateAsync();
        var monitored = await AddMonitoredInterface(database);
        database.Context.Incidents.Add(new Incident
        {
            DeviceId = monitored.Profile.DeviceId,
            SnmpMonitoredInterfaceId = monitored.Id,
            Type = IncidentType.InterfaceInboundBandwidthHigh,
            Status = IncidentStatus.Open,
            Summary = "Inbound bandwidth is high",
            StartedAt = Timestamp(0), CreatedAt = Timestamp(0), UpdatedAt = Timestamp(0)
        });
        await database.Context.SaveChangesAsync();
        var evaluator = CreateEvaluator(database, downTrigger: 1, upRecovery: 2);

        await evaluator.EvaluateAsync(monitored.Id, "Up", "Up", Timestamp(1), CancellationToken.None);
        await evaluator.EvaluateAsync(monitored.Id, "Up", "LowerLayerDown", Timestamp(2), CancellationToken.None);
        await evaluator.EvaluateAsync(monitored.Id, "Up", "Down", Timestamp(3), CancellationToken.None);

        var incidents = await database.Context.Incidents.ToListAsync();
        Assert.Equal(2, incidents.Count);
        Assert.Single(incidents, item => item.Type == IncidentType.InterfaceDown);
        Assert.Single(incidents, item => item.Type == IncidentType.InterfaceInboundBandwidthHigh);
    }

    [Fact]
    public async Task EvaluateAsync_SkipsDisabledMonitoringAndHonorsCancellation()
    {
        await using var database = await TestDatabase.CreateAsync();
        var monitored = await AddMonitoredInterface(database);
        monitored.IsEnabled = false;
        await database.Context.SaveChangesAsync();
        var evaluator = CreateEvaluator(database, downTrigger: 1, upRecovery: 1);

        await evaluator.EvaluateAsync(monitored.Id, "Up", "Down", Timestamp(0), CancellationToken.None);
        Assert.Empty(database.Context.Incidents);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            evaluator.EvaluateAsync(monitored.Id, "Up", "Down", Timestamp(1), cancellation.Token));
    }

    private static InterfaceStatusIncidentEvaluator CreateEvaluator(TestDatabase database, int downTrigger, int upRecovery)
        => new(database.Context, Options.Create(new SnmpBandwidthMonitoringOptions
        {
            InterfaceDownTriggerSamples = downTrigger,
            InterfaceUpRecoverySamples = upRecovery
        }));

    private static DateTimeOffset Timestamp(int minute)
        => new(2026, 7, 31, 9, minute, 0, TimeSpan.Zero);

    private static async Task<SnmpMonitoredInterface> AddMonitoredInterface(TestDatabase database)
    {
        var device = await database.AddDeviceAsync();
        var credential = new NetworkCredential
        {
            Name = "SNMP", Type = NetworkCredentialType.SnmpV2Community, ProtectedSecret = "x",
            CreatedAt = Timestamp(0), UpdatedAt = Timestamp(0)
        };
        var monitored = new SnmpMonitoredInterface
        {
            InterfaceIndex = 7,
            InterfaceName = "Gi0/7",
            IsEnabled = true,
            CreatedAt = Timestamp(0),
            Profile = new SnmpMonitoringProfile
            {
                DeviceId = device.Id,
                Credential = credential,
                IsEnabled = true,
                CreatedAt = Timestamp(0),
                UpdatedAt = Timestamp(0)
            }
        };
        database.Context.Add(monitored);
        await database.Context.SaveChangesAsync();
        return monitored;
    }
}
