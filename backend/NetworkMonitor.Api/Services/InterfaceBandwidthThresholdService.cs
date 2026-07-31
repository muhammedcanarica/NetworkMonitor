using Microsoft.EntityFrameworkCore;
using NetworkMonitor.Api.Data;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Services;

public sealed class InterfaceBandwidthThresholdService(NetworkMonitorDbContext dbContext) : IInterfaceBandwidthThresholdService
{
    private const double BitsPerMegabit = 1_000_000d;
    private const double MaximumThresholdMbps = 1_000_000d;

    public async Task<InterfaceBandwidthThresholdResponse?> GetAsync(int deviceId, int interfaceIndex, CancellationToken cancellationToken)
    {
        var monitoredInterface = await GetMonitoredInterface(deviceId, interfaceIndex, cancellationToken);
        return monitoredInterface.BandwidthThreshold is null ? null : Map(monitoredInterface.BandwidthThreshold, interfaceIndex);
    }

    public async Task<InterfaceBandwidthThresholdResponse> UpdateAsync(
        int deviceId,
        int interfaceIndex,
        UpdateInterfaceBandwidthThresholdRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);
        var monitoredInterface = await GetMonitoredInterface(deviceId, interfaceIndex, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var threshold = monitoredInterface.BandwidthThreshold;
        if (threshold is null)
        {
            threshold = new InterfaceBandwidthThreshold { SnmpMonitoredInterfaceId = monitoredInterface.Id, CreatedAt = now };
            dbContext.InterfaceBandwidthThresholds.Add(threshold);
        }
        threshold.InboundThresholdBitsPerSecond = ToBitsPerSecond(request.InboundThresholdMbps);
        threshold.OutboundThresholdBitsPerSecond = ToBitsPerSecond(request.OutboundThresholdMbps);
        threshold.BreachSampleCount = request.BreachSampleCount;
        threshold.RecoverySampleCount = request.RecoverySampleCount;
        threshold.IsEnabled = request.IsEnabled;
        threshold.InboundConsecutiveBreaches = 0;
        threshold.OutboundConsecutiveBreaches = 0;
        threshold.InboundConsecutiveRecoveries = 0;
        threshold.OutboundConsecutiveRecoveries = 0;
        threshold.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(threshold, interfaceIndex);
    }

    public async Task DeleteAsync(int deviceId, int interfaceIndex, CancellationToken cancellationToken)
    {
        var monitoredInterface = await GetMonitoredInterface(deviceId, interfaceIndex, cancellationToken);
        var threshold = monitoredInterface.BandwidthThreshold
            ?? throw new InterfaceBandwidthThresholdNotFoundException("The bandwidth threshold was not found.");
        var hasOpenIncident = await dbContext.Incidents.AnyAsync(item =>
            item.SnmpMonitoredInterfaceId == monitoredInterface.Id
            && item.Status == IncidentStatus.Open
            && (item.Type == IncidentType.InterfaceInboundBandwidthHigh || item.Type == IncidentType.InterfaceOutboundBandwidthHigh), cancellationToken);
        if (hasOpenIncident)
        {
            throw new InterfaceBandwidthThresholdConflictException("Resolve the open bandwidth incident before deleting this threshold.");
        }
        dbContext.Remove(threshold);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<SnmpMonitoredInterface> GetMonitoredInterface(int deviceId, int interfaceIndex, CancellationToken cancellationToken)
    {
        if (!await dbContext.Devices.AnyAsync(item => item.Id == deviceId, cancellationToken))
            throw new InterfaceBandwidthThresholdNotFoundException("The device was not found.");
        return await dbContext.SnmpMonitoredInterfaces.Include(item => item.BandwidthThreshold)
            .SingleOrDefaultAsync(item => item.Profile.DeviceId == deviceId && item.InterfaceIndex == interfaceIndex && item.IsEnabled, cancellationToken)
            ?? throw new InterfaceBandwidthThresholdNotFoundException("The monitored interface was not found.");
    }

    private static void Validate(UpdateInterfaceBandwidthThresholdRequest request)
    {
        if (!IsValidThreshold(request.InboundThresholdMbps) || !IsValidThreshold(request.OutboundThresholdMbps))
            throw new InterfaceBandwidthThresholdValidationException($"Thresholds must be greater than zero and no more than {MaximumThresholdMbps:N0} Mbps.");
        if (!request.InboundThresholdMbps.HasValue && !request.OutboundThresholdMbps.HasValue)
            throw new InterfaceBandwidthThresholdValidationException("Configure at least one inbound or outbound threshold.");
        if (request.BreachSampleCount is < 1 or > 100 || request.RecoverySampleCount is < 1 or > 100)
            throw new InterfaceBandwidthThresholdValidationException("Trigger and recovery sample counts must be between 1 and 100.");
    }

    private static bool IsValidThreshold(double? value) => !value.HasValue || (double.IsFinite(value.Value) && value.Value > 0 && value.Value <= MaximumThresholdMbps);
    private static double? ToBitsPerSecond(double? megabitsPerSecond) => megabitsPerSecond * BitsPerMegabit;
    private static InterfaceBandwidthThresholdResponse Map(InterfaceBandwidthThreshold item, int interfaceIndex) => new(
        interfaceIndex,
        item.InboundThresholdBitsPerSecond / BitsPerMegabit,
        item.OutboundThresholdBitsPerSecond / BitsPerMegabit,
        item.BreachSampleCount,
        item.RecoverySampleCount,
        item.IsEnabled,
        item.CreatedAt,
        item.UpdatedAt);
}
