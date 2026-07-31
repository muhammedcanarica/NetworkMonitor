using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NetworkMonitor.Api.Data;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Services;

public sealed class NotificationService(NetworkMonitorDbContext dbContext) : INotificationService
{
    public const int DefaultLimit = 50;
    public const int MaximumLimit = 100;

    public async Task<NotificationResponse?> CreateForIncidentAsync(long incidentId, CancellationToken cancellationToken)
    {
        var incident = await dbContext.Incidents
            .AsNoTracking()
            .Include(item => item.Device)
            .Include(item => item.SnmpMonitoredInterface)
            .SingleOrDefaultAsync(item => item.Id == incidentId, cancellationToken);
        if (incident is null || incident.Status != IncidentStatus.Open || !IsSupported(incident.Type)) return null;

        var existing = await dbContext.Notifications.AsNoTracking().SingleOrDefaultAsync(
            item => item.IncidentId == incidentId && item.Type == NotificationType.IncidentOpened,
            cancellationToken);
        if (existing is not null) return ToResponse(existing);

        var (title, message) = BuildContent(incident);
        var notification = new Notification
        {
            Type = NotificationType.IncidentOpened,
            Title = title,
            Message = message,
            IncidentId = incident.Id,
            DeviceId = incident.DeviceId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Notifications.Add(notification);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return ToResponse(notification);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException { SqliteErrorCode: 19 })
        {
            dbContext.Entry(notification).State = EntityState.Detached;
            var concurrent = await dbContext.Notifications.AsNoTracking().SingleOrDefaultAsync(
                item => item.IncidentId == incidentId && item.Type == NotificationType.IncidentOpened,
                cancellationToken);
            if (concurrent is not null) return ToResponse(concurrent);
            throw;
        }
    }

    public async Task<IReadOnlyList<NotificationResponse>> ListAsync(bool unreadOnly, int limit, CancellationToken cancellationToken)
    {
        if (limit is < 1 or > MaximumLimit) throw new ArgumentOutOfRangeException(nameof(limit));
        var query = dbContext.Notifications.AsNoTracking();
        if (unreadOnly) query = query.Where(item => item.ReadAt == null);
        var notifications = await query.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
            .Take(limit).ToListAsync(cancellationToken);
        return notifications.Select(ToResponse).ToList();
    }

    public Task<int> GetUnreadCountAsync(CancellationToken cancellationToken)
        => dbContext.Notifications.CountAsync(item => item.ReadAt == null, cancellationToken);

    public async Task<bool> MarkAsReadAsync(long id, CancellationToken cancellationToken)
    {
        var notification = await dbContext.Notifications.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (notification is null) return false;
        if (notification.ReadAt is null)
        {
            notification.ReadAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        return true;
    }

    public async Task<int> MarkAllAsReadAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        return await dbContext.Notifications.Where(item => item.ReadAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.ReadAt, now), cancellationToken);
    }

    private static bool IsSupported(IncidentType type) => type is
        IncidentType.DeviceUnreachable or
        IncidentType.InterfaceDown or
        IncidentType.InterfaceInboundBandwidthHigh or
        IncidentType.InterfaceOutboundBandwidthHigh;

    private static (string Title, string Message) BuildContent(Incident incident)
    {
        var interfaceName = incident.SnmpMonitoredInterface is null
            ? "the interface"
            : string.IsNullOrWhiteSpace(incident.SnmpMonitoredInterface.InterfaceName)
                ? $"Interface {incident.SnmpMonitoredInterface.InterfaceIndex}"
                : incident.SnmpMonitoredInterface.InterfaceName;
        return incident.Type switch
        {
            IncidentType.DeviceUnreachable => ("Device unreachable", $"{incident.Device.Name} became unreachable."),
            IncidentType.InterfaceDown => ("Interface down", $"{interfaceName} on {incident.Device.Name} is down."),
            IncidentType.InterfaceInboundBandwidthHigh => ("High inbound bandwidth", $"Inbound traffic on {interfaceName} exceeded the configured threshold."),
            IncidentType.InterfaceOutboundBandwidthHigh => ("High outbound bandwidth", $"Outbound traffic on {interfaceName} exceeded the configured threshold."),
            _ => throw new InvalidOperationException("Unsupported incident type for notification.")
        };
    }

    private static NotificationResponse ToResponse(Notification item) => new(
        item.Id, item.Type, item.Title, item.Message, item.IncidentId, item.DeviceId,
        item.CreatedAt, item.ReadAt, item.ReadAt.HasValue);
}
