using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NetworkMonitor.Api.Data;
using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Services;

public sealed class InterfaceBandwidthThresholdEvaluator(NetworkMonitorDbContext dbContext) : IInterfaceBandwidthThresholdEvaluator
{
    public async Task EvaluateAsync(int monitoredInterfaceId, InterfaceTrafficSample sample, CancellationToken cancellationToken)
    {
        var monitoredInterface = await dbContext.SnmpMonitoredInterfaces
            .Include(item => item.Profile)
            .Include(item => item.BandwidthThreshold)
            .SingleOrDefaultAsync(item => item.Id == monitoredInterfaceId, cancellationToken);
        var threshold = monitoredInterface?.BandwidthThreshold;
        if (monitoredInterface is null || threshold is null || !monitoredInterface.IsEnabled || !monitoredInterface.Profile.IsEnabled || !threshold.IsEnabled)
            return;

        if (sample.InBitsPerSecond.HasValue && threshold.InboundThresholdBitsPerSecond.HasValue)
        {
            await EvaluateDirectionAsync(monitoredInterface, threshold, BandwidthDirection.Inbound, sample.InBitsPerSecond.Value, threshold.InboundThresholdBitsPerSecond.Value, sample.Timestamp, cancellationToken);
        }
        if (sample.OutBitsPerSecond.HasValue && threshold.OutboundThresholdBitsPerSecond.HasValue)
        {
            await EvaluateDirectionAsync(monitoredInterface, threshold, BandwidthDirection.Outbound, sample.OutBitsPerSecond.Value, threshold.OutboundThresholdBitsPerSecond.Value, sample.Timestamp, cancellationToken);
        }
    }

    private async Task EvaluateDirectionAsync(
        SnmpMonitoredInterface monitoredInterface,
        InterfaceBandwidthThreshold threshold,
        BandwidthDirection direction,
        double rate,
        double thresholdRate,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        var incidentType = direction == BandwidthDirection.Inbound
            ? IncidentType.InterfaceInboundBandwidthHigh
            : IncidentType.InterfaceOutboundBandwidthHigh;
        var openIncident = await dbContext.Incidents.SingleOrDefaultAsync(item =>
            item.SnmpMonitoredInterfaceId == monitoredInterface.Id
            && item.Type == incidentType
            && item.Status == IncidentStatus.Open, cancellationToken);
        Incident? newIncident = null;

        if (openIncident is null)
        {
            SetRecoveries(threshold, direction, 0);
            var breaches = rate > thresholdRate ? GetBreaches(threshold, direction) + 1 : 0;
            SetBreaches(threshold, direction, breaches);
            if (breaches >= threshold.BreachSampleCount)
            {
                SetBreaches(threshold, direction, 0);
                newIncident = new Incident
                {
                    DeviceId = monitoredInterface.Profile.DeviceId,
                    SnmpMonitoredInterfaceId = monitoredInterface.Id,
                    Type = incidentType,
                    Status = IncidentStatus.Open,
                    Summary = $"{direction} bandwidth threshold exceeded on interface {monitoredInterface.InterfaceIndex}",
                    ThresholdBitsPerSecond = thresholdRate,
                    ObservedBitsPerSecond = rate,
                    StartedAt = timestamp,
                    CreatedAt = timestamp,
                    UpdatedAt = timestamp
                };
                dbContext.Incidents.Add(newIncident);
            }
        }
        else
        {
            SetBreaches(threshold, direction, 0);
            var recoveries = rate <= thresholdRate ? GetRecoveries(threshold, direction) + 1 : 0;
            SetRecoveries(threshold, direction, recoveries);
            if (recoveries >= threshold.RecoverySampleCount)
            {
                SetRecoveries(threshold, direction, 0);
                openIncident.Status = IncidentStatus.Resolved;
                openIncident.ResolvedAt = timestamp;
                openIncident.UpdatedAt = timestamp;
            }
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (newIncident is not null && exception.InnerException is SqliteException { SqliteErrorCode: 19 })
        {
            dbContext.Entry(newIncident).State = EntityState.Detached;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static int GetBreaches(InterfaceBandwidthThreshold threshold, BandwidthDirection direction)
        => direction == BandwidthDirection.Inbound ? threshold.InboundConsecutiveBreaches : threshold.OutboundConsecutiveBreaches;
    private static void SetBreaches(InterfaceBandwidthThreshold threshold, BandwidthDirection direction, int value)
    {
        if (direction == BandwidthDirection.Inbound) threshold.InboundConsecutiveBreaches = value;
        else threshold.OutboundConsecutiveBreaches = value;
    }
    private static int GetRecoveries(InterfaceBandwidthThreshold threshold, BandwidthDirection direction)
        => direction == BandwidthDirection.Inbound ? threshold.InboundConsecutiveRecoveries : threshold.OutboundConsecutiveRecoveries;
    private static void SetRecoveries(InterfaceBandwidthThreshold threshold, BandwidthDirection direction, int value)
    {
        if (direction == BandwidthDirection.Inbound) threshold.InboundConsecutiveRecoveries = value;
        else threshold.OutboundConsecutiveRecoveries = value;
    }
}
