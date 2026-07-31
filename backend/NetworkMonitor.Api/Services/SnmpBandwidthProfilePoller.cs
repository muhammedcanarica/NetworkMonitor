using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetworkMonitor.Api.Configuration;
using NetworkMonitor.Api.Data;
using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Services;

public sealed class SnmpBandwidthProfilePoller(
    NetworkMonitorDbContext dbContext,
    INetworkOperationCredentialResolver credentialResolver,
    ISnmpBandwidthProbe probe,
    IInterfaceBandwidthThresholdEvaluator thresholdEvaluator,
    IInterfaceStatusIncidentEvaluator statusEvaluator,
    IOptions<SnmpBandwidthMonitoringOptions> options,
    ILogger<SnmpBandwidthProfilePoller> logger) : ISnmpBandwidthProfilePoller
{
    private readonly SnmpBandwidthMonitoringOptions _options = options.Value;

    public async Task PollAsync(int profileId, CancellationToken cancellationToken)
    {
        var profile = await dbContext.SnmpMonitoringProfiles
            .Include(item => item.Device)
            .Include(item => item.Interfaces)
            .SingleOrDefaultAsync(item => item.Id == profileId && item.IsEnabled, cancellationToken);
        if (profile is null) return;

        var interfaces = profile.Interfaces.Where(item => item.IsEnabled).ToList();
        if (interfaces.Count == 0) return;

        var community = await credentialResolver.ResolveSnmpCommunityAsync(null, profile.CredentialId, cancellationToken);
        var readings = await probe.ReadAsync(
            profile.Device.IpAddress,
            community,
            interfaces.Select(item => item.InterfaceIndex).ToArray(),
            _options.RequestTimeoutMilliseconds,
            cancellationToken);
        var readingsByIndex = readings.ToDictionary(item => item.InterfaceIndex);
        var timestamp = DateTimeOffset.UtcNow;
        var maximumGap = TimeSpan.FromSeconds(_options.IntervalSeconds * 3L);
        var persistedSamples = new List<InterfaceTrafficSample>();

        foreach (var monitoredInterface in interfaces)
        {
            if (!readingsByIndex.TryGetValue(monitoredInterface.InterfaceIndex, out var reading))
            {
                logger.LogWarning("Interface {InterfaceIndex} on device {DeviceId} does not expose supported 64-bit traffic counters.", monitoredInterface.InterfaceIndex, profile.DeviceId);
                continue;
            }

            var previous = await dbContext.InterfaceTrafficSamples
                .Where(item => item.SnmpMonitoredInterfaceId == monitoredInterface.Id)
                .OrderByDescending(item => item.Timestamp)
                .FirstOrDefaultAsync(cancellationToken);
            var rates = InterfaceTrafficRateCalculator.Calculate(previous, reading, timestamp, maximumGap);
            var sample = new InterfaceTrafficSample
            {
                SnmpMonitoredInterfaceId = monitoredInterface.Id,
                Timestamp = timestamp,
                InOctets = reading.InOctets,
                OutOctets = reading.OutOctets,
                InBitsPerSecond = rates.InBitsPerSecond,
                OutBitsPerSecond = rates.OutBitsPerSecond,
                OperStatus = reading.OperStatus ?? "Unknown",
                AdminStatus = reading.AdminStatus,
                SysUpTimeTicks = reading.SysUpTimeTicks,
                CounterDiscontinuityTicks = reading.CounterDiscontinuityTicks
            };
            dbContext.InterfaceTrafficSamples.Add(sample);
            persistedSamples.Add(sample);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        foreach (var sample in persistedSamples)
        {
            try
            {
                await thresholdEvaluator.EvaluateAsync(sample.SnmpMonitoredInterfaceId, sample, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning("Bandwidth threshold evaluation failed for interface {InterfaceId} ({ErrorType}).", sample.SnmpMonitoredInterfaceId, exception.GetType().Name);
            }
        }
        foreach (var sample in persistedSamples)
        {
            try
            {
                await statusEvaluator.EvaluateAsync(sample.SnmpMonitoredInterfaceId, sample.AdminStatus, sample.OperStatus, sample.Timestamp, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                logger.LogWarning("Interface status evaluation failed for interface {InterfaceId} ({ErrorType}).", sample.SnmpMonitoredInterfaceId, exception.GetType().Name);
            }
        }
    }
}
