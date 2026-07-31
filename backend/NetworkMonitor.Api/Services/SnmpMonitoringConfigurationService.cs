using Microsoft.EntityFrameworkCore;
using NetworkMonitor.Api.Data;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Services;

public sealed class SnmpMonitoringConfigurationService(
    NetworkMonitorDbContext dbContext,
    INetworkOperationCredentialResolver credentialResolver,
    ISnmpService snmpService) : ISnmpMonitoringConfigurationService
{
    private static readonly int[] SupportedHistoryHours = [1, 6, 24, 168];
    private const int MaximumHistorySamples = 2000;

    public async Task<SnmpMonitoringProfileResponse?> GetAsync(int deviceId, CancellationToken cancellationToken)
    {
        await EnsureDeviceExists(deviceId, cancellationToken);
        var profile = await dbContext.SnmpMonitoringProfiles.AsNoTracking().Include(item => item.Interfaces)
            .SingleOrDefaultAsync(item => item.DeviceId == deviceId, cancellationToken);
        return profile is null ? null : MapProfile(profile);
    }

    public async Task<IReadOnlyList<SnmpInterfaceResponse>> DiscoverInterfacesAsync(
        int deviceId,
        DiscoverMonitoringInterfacesRequest request,
        CancellationToken cancellationToken)
    {
        var device = await GetDevice(deviceId, cancellationToken);
        var community = await credentialResolver.ResolveSnmpCommunityAsync(null, request.CredentialId, cancellationToken);
        return await snmpService.GetInterfacesAsync(device.IpAddress, community, request.TimeoutMilliseconds, cancellationToken);
    }

    public async Task<SnmpMonitoringProfileResponse> UpdateAsync(
        int deviceId,
        UpdateSnmpMonitoringRequest request,
        CancellationToken cancellationToken)
    {
        var indexes = request.InterfaceIndexes.Distinct().ToArray();
        if (indexes.Length != request.InterfaceIndexes.Count || indexes.Any(index => index <= 0) || indexes.Length > 128)
        {
            throw new SnmpMonitoringValidationException("Choose between 1 and 128 unique, positive interface indexes.");
        }

        var device = await GetDevice(deviceId, cancellationToken);
        var community = await credentialResolver.ResolveSnmpCommunityAsync(null, request.CredentialId, cancellationToken);
        var discovered = await snmpService.GetInterfacesAsync(device.IpAddress, community, 5000, cancellationToken);
        var discoveredByIndex = discovered.ToDictionary(item => item.Index);
        if (indexes.Any(index => !discoveredByIndex.ContainsKey(index)))
        {
            throw new SnmpMonitoringValidationException("One or more selected interfaces are no longer available on the device.");
        }

        var now = DateTimeOffset.UtcNow;
        var profile = await dbContext.SnmpMonitoringProfiles.Include(item => item.Interfaces)
            .SingleOrDefaultAsync(item => item.DeviceId == deviceId, cancellationToken);
        if (profile is null)
        {
            profile = new SnmpMonitoringProfile { DeviceId = deviceId, CreatedAt = now };
            dbContext.SnmpMonitoringProfiles.Add(profile);
        }

        profile.CredentialId = request.CredentialId;
        profile.IsEnabled = request.IsEnabled;
        profile.UpdatedAt = now;
        foreach (var item in profile.Interfaces)
        {
            item.IsEnabled = indexes.Contains(item.InterfaceIndex);
        }
        foreach (var index in indexes)
        {
            var discoveredInterface = discoveredByIndex[index];
            var item = profile.Interfaces.SingleOrDefault(existing => existing.InterfaceIndex == index);
            if (item is null)
            {
                item = new SnmpMonitoredInterface { InterfaceIndex = index, CreatedAt = now };
                profile.Interfaces.Add(item);
            }
            item.InterfaceName = string.IsNullOrWhiteSpace(discoveredInterface.Name)
                ? discoveredInterface.Description ?? $"Interface {index}"
                : discoveredInterface.Name;
            item.Description = discoveredInterface.Description;
            item.IsEnabled = true;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return MapProfile(profile);
    }

    public async Task DisableAsync(int deviceId, CancellationToken cancellationToken)
    {
        await EnsureDeviceExists(deviceId, cancellationToken);
        var profile = await dbContext.SnmpMonitoringProfiles.SingleOrDefaultAsync(item => item.DeviceId == deviceId, cancellationToken)
            ?? throw new SnmpMonitoringNotFoundException("SNMP monitoring is not configured for this device.");
        profile.IsEnabled = false;
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InterfaceTrafficSummaryResponse>> GetSummaryAsync(int deviceId, CancellationToken cancellationToken)
    {
        await EnsureDeviceExists(deviceId, cancellationToken);
        var interfaces = await dbContext.SnmpMonitoredInterfaces.AsNoTracking().Include(item => item.BandwidthThreshold)
            .Where(item => item.Profile.DeviceId == deviceId && item.IsEnabled)
            .OrderBy(item => item.InterfaceIndex)
            .ToListAsync(cancellationToken);
        var result = new List<InterfaceTrafficSummaryResponse>(interfaces.Count);
        var interfaceIds = interfaces.Select(item => item.Id).ToArray();
        var openIncidents = await dbContext.Incidents.AsNoTracking()
            .Where(item => item.Status == IncidentStatus.Open && item.SnmpMonitoredInterfaceId.HasValue && interfaceIds.Contains(item.SnmpMonitoredInterfaceId.Value))
            .Select(item => new { item.SnmpMonitoredInterfaceId, item.Type })
            .ToListAsync(cancellationToken);
        foreach (var item in interfaces)
        {
            var sample = await dbContext.InterfaceTrafficSamples.AsNoTracking()
                .Where(value => value.SnmpMonitoredInterfaceId == item.Id)
                .OrderByDescending(value => value.Timestamp)
                .FirstOrDefaultAsync(cancellationToken);
            result.Add(new InterfaceTrafficSummaryResponse(
                item.InterfaceIndex,
                item.InterfaceName,
                item.Description,
                sample?.OperStatus,
                sample?.Timestamp,
                sample?.InBitsPerSecond,
                sample?.OutBitsPerSecond,
                item.BandwidthThreshold is null ? null : new InterfaceBandwidthThresholdResponse(
                    item.InterfaceIndex,
                    item.BandwidthThreshold.InboundThresholdBitsPerSecond / 1_000_000d,
                    item.BandwidthThreshold.OutboundThresholdBitsPerSecond / 1_000_000d,
                    item.BandwidthThreshold.BreachSampleCount,
                    item.BandwidthThreshold.RecoverySampleCount,
                    item.BandwidthThreshold.IsEnabled,
                    item.BandwidthThreshold.CreatedAt,
                    item.BandwidthThreshold.UpdatedAt),
                openIncidents.Any(incident => incident.SnmpMonitoredInterfaceId == item.Id && incident.Type == IncidentType.InterfaceInboundBandwidthHigh),
                openIncidents.Any(incident => incident.SnmpMonitoredInterfaceId == item.Id && incident.Type == IncidentType.InterfaceOutboundBandwidthHigh)));
        }
        return result;
    }

    public async Task<InterfaceTrafficHistoryResponse> GetHistoryAsync(
        int deviceId,
        int interfaceIndex,
        int hours,
        CancellationToken cancellationToken)
    {
        if (!SupportedHistoryHours.Contains(hours))
        {
            throw new SnmpMonitoringValidationException("History range must be 1, 6, 24, or 168 hours.");
        }
        await EnsureDeviceExists(deviceId, cancellationToken);
        var monitoredInterface = await dbContext.SnmpMonitoredInterfaces.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Profile.DeviceId == deviceId && item.InterfaceIndex == interfaceIndex, cancellationToken)
            ?? throw new SnmpMonitoringNotFoundException("The monitored interface was not found.");
        var cutoff = DateTimeOffset.UtcNow.AddHours(-hours);
        var samples = await dbContext.InterfaceTrafficSamples.AsNoTracking()
            .Where(item => item.SnmpMonitoredInterfaceId == monitoredInterface.Id && item.Timestamp >= cutoff)
            .OrderByDescending(item => item.Timestamp)
            .Take(MaximumHistorySamples)
            .ToListAsync(cancellationToken);
        samples.Reverse();
        return new InterfaceTrafficHistoryResponse(interfaceIndex, monitoredInterface.InterfaceName, hours,
            samples.Select(item => new InterfaceTrafficSampleResponse(item.Timestamp, item.InOctets, item.OutOctets, item.InBitsPerSecond, item.OutBitsPerSecond, item.OperStatus)).ToList());
    }

    private async Task<Device> GetDevice(int deviceId, CancellationToken cancellationToken)
        => await dbContext.Devices.AsNoTracking().SingleOrDefaultAsync(item => item.Id == deviceId, cancellationToken)
            ?? throw new SnmpMonitoringNotFoundException("The device was not found.");

    private async Task EnsureDeviceExists(int deviceId, CancellationToken cancellationToken) => _ = await GetDevice(deviceId, cancellationToken);

    private static SnmpMonitoringProfileResponse MapProfile(SnmpMonitoringProfile profile) => new(
        profile.DeviceId, profile.CredentialId, profile.IsEnabled, profile.CreatedAt, profile.UpdatedAt,
        profile.Interfaces.OrderBy(item => item.InterfaceIndex).Select(item => new SnmpMonitoredInterfaceResponse(item.InterfaceIndex, item.InterfaceName, item.Description, item.IsEnabled)).ToList());
}
