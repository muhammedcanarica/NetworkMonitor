using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Models;
using NetworkMonitor.Api.Services;
using NetworkMonitor.Api.Tests.Infrastructure;

namespace NetworkMonitor.Api.Tests.Services;

public sealed class NotificationServiceTests
{
    [Theory]
    [InlineData(IncidentType.DeviceUnreachable, "Device unreachable", "Test Device became unreachable.")]
    [InlineData(IncidentType.InterfaceDown, "Interface down", "Gi0/7 on Test Device is down.")]
    [InlineData(IncidentType.InterfaceInboundBandwidthHigh, "High inbound bandwidth", "Inbound traffic on Gi0/7 exceeded the configured threshold.")]
    [InlineData(IncidentType.InterfaceOutboundBandwidthHigh, "High outbound bandwidth", "Outbound traffic on Gi0/7 exceeded the configured threshold.")]
    public async Task CreateForIncidentAsync_CreatesExpectedIncidentOpenedNotification(
        IncidentType incidentType,
        string expectedTitle,
        string expectedMessage)
    {
        await using var database = await TestDatabase.CreateAsync();
        var incident = await AddIncident(database, incidentType);

        var response = await new NotificationService(database.Context)
            .CreateForIncidentAsync(incident.Id, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(NotificationType.IncidentOpened, response!.Type);
        Assert.Equal(expectedTitle, response.Title);
        Assert.Equal(expectedMessage, response.Message);
        Assert.Equal(incident.Id, response.IncidentId);
        Assert.Equal(incident.DeviceId, response.DeviceId);
        Assert.Equal(TimeSpan.Zero, response.CreatedAt.Offset);
        Assert.False(response.IsRead);
    }

    [Fact]
    public async Task CreateForIncidentAsync_IsIdempotentAndRecoveryDoesNotCreateAnotherNotification()
    {
        await using var database = await TestDatabase.CreateAsync();
        var incident = await AddIncident(database, IncidentType.DeviceUnreachable);
        var service = new NotificationService(database.Context);

        var first = await service.CreateForIncidentAsync(incident.Id, CancellationToken.None);
        var duplicate = await service.CreateForIncidentAsync(incident.Id, CancellationToken.None);
        incident.Status = IncidentStatus.Resolved;
        incident.ResolvedAt = DateTimeOffset.UtcNow;
        await database.Context.SaveChangesAsync();
        var recoveryAttempt = await service.CreateForIncidentAsync(incident.Id, CancellationToken.None);

        Assert.Equal(first!.Id, duplicate!.Id);
        Assert.Null(recoveryAttempt);
        Assert.Single(await database.Context.Notifications.ToListAsync());
    }

    [Fact]
    public async Task DatabaseConstraint_RejectsDuplicateIncidentOpenedNotification()
    {
        await using var database = await TestDatabase.CreateAsync();
        var incident = await AddIncident(database, IncidentType.DeviceUnreachable);
        database.Context.Notifications.AddRange(
            Notification(incident, DateTimeOffset.UtcNow),
            Notification(incident, DateTimeOffset.UtcNow.AddSeconds(1)));

        await Assert.ThrowsAsync<DbUpdateException>(() => database.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task ListReadAndCountOperations_AreOrderedFilteredLimitedAndIdempotent()
    {
        await using var database = await TestDatabase.CreateAsync();
        var incident = await AddIncident(database, IncidentType.DeviceUnreachable);
        var oldest = Notification(incident, DateTimeOffset.UtcNow.AddMinutes(-3));
        var read = Notification(incident, DateTimeOffset.UtcNow.AddMinutes(-2), NotificationType.IncidentOpened, DateTimeOffset.UtcNow.AddMinutes(-1));
        read.IncidentId = null;
        var newest = Notification(incident, DateTimeOffset.UtcNow.AddMinutes(-1));
        newest.IncidentId = null;
        database.Context.Notifications.AddRange(oldest, read, newest);
        await database.Context.SaveChangesAsync();
        var service = new NotificationService(database.Context);

        var limited = await service.ListAsync(false, 2, CancellationToken.None);
        var unread = await service.ListAsync(true, 100, CancellationToken.None);

        Assert.Equal([newest.Id, read.Id], limited.Select(item => item.Id));
        Assert.Equal(2, unread.Count);
        Assert.Equal(2, await service.GetUnreadCountAsync(CancellationToken.None));
        Assert.True(await service.MarkAsReadAsync(oldest.Id, CancellationToken.None));
        var firstReadAt = (await database.Context.Notifications.FindAsync(oldest.Id))!.ReadAt;
        Assert.True(await service.MarkAsReadAsync(oldest.Id, CancellationToken.None));
        Assert.Equal(firstReadAt, (await database.Context.Notifications.FindAsync(oldest.Id))!.ReadAt);
        Assert.False(await service.MarkAsReadAsync(999, CancellationToken.None));
        Assert.Equal(1, await service.MarkAllAsReadAsync(CancellationToken.None));
        Assert.Equal(0, await service.GetUnreadCountAsync(CancellationToken.None));
        Assert.Equal(0, await service.MarkAllAsReadAsync(CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.ListAsync(false, 101, CancellationToken.None));
    }

    [Fact]
    public async Task PublisherFailure_DoesNotLoseIncidentOrEscapeMonitoringFlow()
    {
        await using var database = await TestDatabase.CreateAsync();
        var device = await database.AddDeviceAsync();
        var publisher = new IncidentNotificationPublisher(
            new ThrowingNotificationService(),
            new StubNotificationDeliveryPlanner(),
            NullLogger<IncidentNotificationPublisher>.Instance);

        await new IncidentService(database.Context, publisher)
            .HandleStatusTransitionAsync(device.Id, DeviceStatus.Up, DeviceStatus.Down, CancellationToken.None);

        Assert.Single(await database.Context.Incidents.ToListAsync());
        Assert.Empty(await database.Context.Notifications.ToListAsync());
    }

    [Fact]
    public async Task DeletingDevice_PreservesNotificationSnapshotAndClearsRelationships()
    {
        await using var database = await TestDatabase.CreateAsync();
        var incident = await AddIncident(database, IncidentType.DeviceUnreachable);
        var created = await new NotificationService(database.Context)
            .CreateForIncidentAsync(incident.Id, CancellationToken.None);

        database.Context.Devices.Remove(await database.Context.Devices.SingleAsync(item => item.Id == incident.DeviceId));
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();

        var notification = await database.Context.Notifications.SingleAsync(item => item.Id == created!.Id);
        Assert.Null(notification.DeviceId);
        Assert.Null(notification.IncidentId);
        Assert.Equal("Test Device became unreachable.", notification.Message);
    }

    private static async Task<Incident> AddIncident(TestDatabase database, IncidentType type)
    {
        var device = await database.AddDeviceAsync();
        SnmpMonitoredInterface? monitored = null;
        if (type != IncidentType.DeviceUnreachable)
        {
            monitored = new SnmpMonitoredInterface
            {
                InterfaceIndex = 7,
                InterfaceName = "Gi0/7",
                IsEnabled = true,
                CreatedAt = DateTimeOffset.UtcNow,
                Profile = new SnmpMonitoringProfile
                {
                    DeviceId = device.Id,
                    Credential = new NetworkCredential
                    {
                        Name = $"SNMP-{Guid.NewGuid():N}",
                        Type = NetworkCredentialType.SnmpV2Community,
                        ProtectedSecret = "protected",
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow
                    },
                    IsEnabled = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                }
            };
        }
        var now = DateTimeOffset.UtcNow;
        var incident = new Incident
        {
            DeviceId = device.Id,
            SnmpMonitoredInterface = monitored,
            Type = type,
            Status = IncidentStatus.Open,
            Summary = "Test incident",
            StartedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
        database.Context.Add(incident);
        await database.Context.SaveChangesAsync();
        return incident;
    }

    private static Notification Notification(
        Incident incident,
        DateTimeOffset createdAt,
        NotificationType type = NotificationType.IncidentOpened,
        DateTimeOffset? readAt = null) => new()
    {
        Type = type,
        Title = "Device unreachable",
        Message = "Test Device became unreachable.",
        IncidentId = incident.Id,
        DeviceId = incident.DeviceId,
        CreatedAt = createdAt,
        ReadAt = readAt
    };

    private sealed class ThrowingNotificationService : INotificationService
    {
        public Task<NotificationResponse?> CreateForIncidentAsync(long incidentId, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Notification database unavailable.");
        public Task<IReadOnlyList<NotificationResponse>> ListAsync(bool unreadOnly, int limit, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> GetUnreadCountAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> MarkAsReadAsync(long id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> MarkAllAsReadAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
