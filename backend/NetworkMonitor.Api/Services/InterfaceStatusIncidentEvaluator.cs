using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetworkMonitor.Api.Configuration;
using NetworkMonitor.Api.Data;
using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Services;

public sealed class InterfaceStatusIncidentEvaluator(
    NetworkMonitorDbContext dbContext,
    IOptions<SnmpBandwidthMonitoringOptions> options) : IInterfaceStatusIncidentEvaluator
{
    private readonly SnmpBandwidthMonitoringOptions _options = options.Value;

    public async Task EvaluateAsync(int monitoredInterfaceId, string? adminStatus, string? operStatus, DateTimeOffset timestamp, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(adminStatus) || string.IsNullOrWhiteSpace(operStatus))
            return;
        var monitored = await dbContext.SnmpMonitoredInterfaces.Include(item => item.Profile)
            .SingleOrDefaultAsync(item => item.Id == monitoredInterfaceId, cancellationToken);
        if (monitored is null || !monitored.IsEnabled || !monitored.Profile.IsEnabled) return;
        if (!string.Equals(adminStatus, "Up", StringComparison.OrdinalIgnoreCase))
        {
            monitored.LastOperationalState = InterfaceOperationalState.Neutral;
            monitored.ConsecutiveDownSamples = 0;
            monitored.ConsecutiveUpSamples = 0;
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var state = ClassifyOperStatus(operStatus);
        if (monitored.LastOperationalState is null)
        {
            monitored.LastOperationalState = state;
            monitored.ConsecutiveDownSamples = 0;
            monitored.ConsecutiveUpSamples = 0;
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var openIncident = await dbContext.Incidents.SingleOrDefaultAsync(item => item.SnmpMonitoredInterfaceId == monitored.Id && item.Type == IncidentType.InterfaceDown && item.Status == IncidentStatus.Open, cancellationToken);
        Incident? newIncident = null;
        switch (state)
        {
            case InterfaceOperationalState.Problem:
                monitored.ConsecutiveUpSamples = 0;
                if (openIncident is null)
                {
                    monitored.ConsecutiveDownSamples++;
                    if (monitored.ConsecutiveDownSamples >= _options.InterfaceDownTriggerSamples)
                    {
                        monitored.ConsecutiveDownSamples = 0;
                        newIncident = new Incident
                        {
                            DeviceId = monitored.Profile.DeviceId,
                            SnmpMonitoredInterfaceId = monitored.Id,
                            Type = IncidentType.InterfaceDown,
                            Status = IncidentStatus.Open,
                            Summary = $"Interface {(!string.IsNullOrWhiteSpace(monitored.InterfaceName) ? monitored.InterfaceName : monitored.InterfaceIndex)} is down",
                            StartedAt = timestamp,
                            CreatedAt = timestamp,
                            UpdatedAt = timestamp
                        };
                        dbContext.Incidents.Add(newIncident);
                    }
                }
                break;
            case InterfaceOperationalState.Up:
                monitored.ConsecutiveDownSamples = 0;
                if (openIncident is not null)
                {
                    monitored.ConsecutiveUpSamples++;
                    if (monitored.ConsecutiveUpSamples >= _options.InterfaceUpRecoverySamples)
                    {
                        monitored.ConsecutiveUpSamples = 0;
                        openIncident.Status = IncidentStatus.Resolved;
                        openIncident.ResolvedAt = timestamp;
                        openIncident.UpdatedAt = timestamp;
                    }
                }
                else monitored.ConsecutiveUpSamples = 0;
                break;
            default:
                monitored.ConsecutiveDownSamples = 0;
                monitored.ConsecutiveUpSamples = 0;
                break;
        }
        monitored.LastOperationalState = state;
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (newIncident is not null && exception.InnerException is SqliteException { SqliteErrorCode: 19 })
        {
            dbContext.Entry(newIncident).State = EntityState.Detached;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public static InterfaceOperationalState ClassifyOperStatus(string? operStatus) => operStatus?.Trim().ToLowerInvariant() switch
    {
        "up" => InterfaceOperationalState.Up,
        "down" or "lowerlayerdown" => InterfaceOperationalState.Problem,
        _ => InterfaceOperationalState.Neutral
    };
}
