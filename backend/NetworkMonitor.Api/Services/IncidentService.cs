using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using NetworkMonitor.Api.Data;
using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Services;

public sealed class IncidentService(NetworkMonitorDbContext dbContext) : IIncidentService
{
    public async Task HandleStatusTransitionAsync(
        int deviceId,
        DeviceStatus previousStatus,
        DeviceStatus currentStatus,
        CancellationToken cancellationToken)
    {
        if (previousStatus == currentStatus) return;

        if (currentStatus == DeviceStatus.Down)
        {
            await OpenDeviceUnreachableAsync(deviceId, cancellationToken);
        }
        else if (previousStatus == DeviceStatus.Down && currentStatus == DeviceStatus.Up)
        {
            await ResolveDeviceUnreachableAsync(deviceId, cancellationToken);
        }
    }

    private async Task OpenDeviceUnreachableAsync(int deviceId, CancellationToken cancellationToken)
    {
        if (await dbContext.Incidents.AnyAsync(item => item.DeviceId == deviceId
                && item.Type == IncidentType.DeviceUnreachable
                && item.Status == IncidentStatus.Open, cancellationToken)) return;

        var now = DateTimeOffset.UtcNow;
        var incident = new Incident
        {
            DeviceId = deviceId,
            Type = IncidentType.DeviceUnreachable,
            Status = IncidentStatus.Open,
            Summary = "Device became unreachable",
            StartedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.Incidents.Add(incident);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException { SqliteErrorCode: 19 })
        {
            // The filtered unique index wins a concurrent create race; retain the existing incident.
            dbContext.Entry(incident).State = EntityState.Detached;
        }
    }

    private async Task ResolveDeviceUnreachableAsync(int deviceId, CancellationToken cancellationToken)
    {
        var openIncidents = await dbContext.Incidents
            .Where(item => item.DeviceId == deviceId && item.Type == IncidentType.DeviceUnreachable && item.Status == IncidentStatus.Open)
            .ToListAsync(cancellationToken);
        if (openIncidents.Count == 0) return;

        var now = DateTimeOffset.UtcNow;
        foreach (var incident in openIncidents)
        {
            incident.Status = IncidentStatus.Resolved;
            incident.ResolvedAt = now;
            incident.UpdatedAt = now;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
