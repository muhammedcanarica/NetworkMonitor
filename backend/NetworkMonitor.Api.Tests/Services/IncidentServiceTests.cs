using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NetworkMonitor.Api.Models;
using NetworkMonitor.Api.Services;
using NetworkMonitor.Api.Tests.Infrastructure;

namespace NetworkMonitor.Api.Tests.Services;

public sealed class IncidentServiceTests
{
    [Fact]
    public async Task ThresholdTransitions_OpenAndResolveAtTheExistingTrackerBoundaries()
    {
        await using var database = await TestDatabase.CreateAsync();
        var device = await database.AddDeviceAsync();
        var service = new IncidentService(database.Context, CreatePublisher(database));
        var tracker = new DeviceStatusTracker();
        var status = DeviceStatus.Up;

        var firstFailure = tracker.ApplyResult(device.Id, status, false, failureThreshold: 2, recoveryThreshold: 2);
        await service.HandleStatusTransitionAsync(device.Id, status, firstFailure.Status, CancellationToken.None);
        status = firstFailure.Status;
        Assert.Empty(await database.Context.Incidents.ToListAsync());

        var offline = tracker.ApplyResult(device.Id, status, false, failureThreshold: 2, recoveryThreshold: 2);
        await service.HandleStatusTransitionAsync(device.Id, status, offline.Status, CancellationToken.None);
        status = offline.Status;
        Assert.Single(await database.Context.Incidents.Where(item => item.Status == IncidentStatus.Open).ToListAsync());

        var firstRecovery = tracker.ApplyResult(device.Id, status, true, failureThreshold: 2, recoveryThreshold: 2);
        await service.HandleStatusTransitionAsync(device.Id, status, firstRecovery.Status, CancellationToken.None);
        status = firstRecovery.Status;
        Assert.Single(await database.Context.Incidents.Where(item => item.Status == IncidentStatus.Open).ToListAsync());

        var recovered = tracker.ApplyResult(device.Id, status, true, failureThreshold: 2, recoveryThreshold: 2);
        await service.HandleStatusTransitionAsync(device.Id, status, recovered.Status, CancellationToken.None);
        Assert.Single(await database.Context.Incidents.Where(item => item.Status == IncidentStatus.Resolved).ToListAsync());
    }

    [Fact]
    public async Task Transition_OpensOnlyAfterConfirmedDownTransitionAndResolvesAfterUp()
    {
        await using var database = await TestDatabase.CreateAsync();
        var device = await database.AddDeviceAsync();
        var service = new IncidentService(database.Context, CreatePublisher(database));

        await service.HandleStatusTransitionAsync(device.Id, DeviceStatus.Up, DeviceStatus.Warning, CancellationToken.None);
        Assert.Empty(await database.Context.Incidents.ToListAsync());

        await service.HandleStatusTransitionAsync(device.Id, DeviceStatus.Warning, DeviceStatus.Down, CancellationToken.None);
        var incident = Assert.Single(await database.Context.Incidents.ToListAsync());
        Assert.Equal(IncidentStatus.Open, incident.Status);
        Assert.Null(incident.ResolvedAt);
        Assert.Single(await database.Context.Notifications.ToListAsync());

        await service.HandleStatusTransitionAsync(device.Id, DeviceStatus.Down, DeviceStatus.Down, CancellationToken.None);
        await service.HandleStatusTransitionAsync(device.Id, DeviceStatus.Down, DeviceStatus.Warning, CancellationToken.None);
        Assert.Single(await database.Context.Incidents.ToListAsync());
        Assert.Equal(IncidentStatus.Open, incident.Status);

        await service.HandleStatusTransitionAsync(device.Id, DeviceStatus.Down, DeviceStatus.Up, CancellationToken.None);
        Assert.Equal(IncidentStatus.Resolved, incident.Status);
        Assert.NotNull(incident.ResolvedAt);
        Assert.True(incident.ResolvedAt >= incident.StartedAt);
        Assert.Single(await database.Context.Notifications.ToListAsync());
    }

    [Fact]
    public async Task Transition_PreservesExistingOpenIncidentAcrossRestartLikeServiceRecreation()
    {
        await using var database = await TestDatabase.CreateAsync();
        var device = await database.AddDeviceAsync();
        await new IncidentService(database.Context, new StubIncidentNotificationPublisher()).HandleStatusTransitionAsync(device.Id, DeviceStatus.Unknown, DeviceStatus.Down, CancellationToken.None);

        await new IncidentService(database.Context, new StubIncidentNotificationPublisher()).HandleStatusTransitionAsync(device.Id, DeviceStatus.Unknown, DeviceStatus.Down, CancellationToken.None);

        Assert.Single(await database.Context.Incidents.Where(item => item.Status == IncidentStatus.Open).ToListAsync());
    }

    [Fact]
    public async Task Transition_RespectsCancellation()
    {
        await using var database = await TestDatabase.CreateAsync();
        var device = await database.AddDeviceAsync();
        var service = new IncidentService(database.Context, new StubIncidentNotificationPublisher());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.HandleStatusTransitionAsync(device.Id, DeviceStatus.Up, DeviceStatus.Down, cancellation.Token));
    }

    private static IncidentNotificationPublisher CreatePublisher(TestDatabase database)
        => new(new NotificationService(database.Context), NullLogger<IncidentNotificationPublisher>.Instance);
}
