using System.Diagnostics;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetworkMonitor.Api.Configuration;
using NetworkMonitor.Api.Data;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Services;

public sealed class TopologyDiscoveryService(
    NetworkMonitorDbContext dbContext,
    ISnmpService snmpService,
    IOptions<TopologyDiscoveryOptions> options) : ITopologyDiscoveryService
{
    private readonly TopologyDiscoveryOptions _options = options.Value;

    public async Task<TopologyDiscoveryResponse> DiscoverAsync(
        TopologyDiscoveryRequest request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        var community = request.Community!;
        var deviceIds = request.DeviceIds.Distinct().ToArray();
        var allManagedDevices = await dbContext.Devices
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var devices = allManagedDevices.Where(device => deviceIds.Contains(device.Id)).ToList();
        if (devices.Count != deviceIds.Length)
        {
            throw new TopologyDiscoveryValidationException("One or more selected devices were not found.");
        }

        var stopwatch = Stopwatch.StartNew();
        var discoveries = new DeviceDiscovery[devices.Count];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, devices.Count),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = _options.MaxConcurrentDiscoveries,
                CancellationToken = cancellationToken
            },
            async (index, token) => discoveries[index] = await DiscoverDeviceAsync(devices[index], request, community, token));

        var managedDevicesByIp = allManagedDevices.ToDictionary(device => device.IpAddress, StringComparer.Ordinal);
        var nodes = devices
            .Select(ToManagedNode)
            .ToDictionary(node => node.Id, StringComparer.Ordinal);
        var edges = new Dictionary<string, TopologyEdgeResponse>(StringComparer.Ordinal);
        var warnings = new List<string>();

        foreach (var discovery in discoveries)
        {
            if (discovery.Warning is not null)
            {
                warnings.Add(discovery.Warning);
                continue;
            }

            foreach (var neighbor in discovery.Neighbors)
            {
                var target = CreateNeighborNode(discovery.Device, neighbor, managedDevicesByIp);
                nodes.TryAdd(target.Id, target);
                var edge = new TopologyEdgeResponse(
                    string.Empty,
                    $"device:{discovery.Device.Id}",
                    target.Id,
                    neighbor.LocalPort,
                    neighbor.RemotePort,
                    "LLDP");
                var key = CreateNormalizedEdgeKey(edge);
                edges.TryAdd(key, edge with { Id = $"lldp:{edges.Count + 1}" });
            }
        }

        stopwatch.Stop();
        return new TopologyDiscoveryResponse(
            nodes.Values.OrderBy(node => node.IsManaged ? 0 : 1).ThenBy(node => node.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            edges.Values.ToList(),
            devices.Count,
            discoveries.Count(discovery => discovery.Warning is null),
            discoveries.Count(discovery => discovery.Warning is not null),
            stopwatch.ElapsedMilliseconds,
            warnings);
    }

    private async Task<DeviceDiscovery> DiscoverDeviceAsync(
        Device device,
        TopologyDiscoveryRequest request,
        string community,
        CancellationToken cancellationToken)
    {
        try
        {
            var localPortsTask = snmpService.WalkAsync(device.IpAddress, community, SnmpOids.Lldp.LocalPortId, request.TimeoutMilliseconds, cancellationToken);
            var chassisTask = snmpService.WalkAsync(device.IpAddress, community, SnmpOids.Lldp.RemoteChassisId, request.TimeoutMilliseconds, cancellationToken);
            var remotePortTask = snmpService.WalkAsync(device.IpAddress, community, SnmpOids.Lldp.RemotePortId, request.TimeoutMilliseconds, cancellationToken);
            var remotePortDescriptionTask = snmpService.WalkAsync(device.IpAddress, community, SnmpOids.Lldp.RemotePortDescription, request.TimeoutMilliseconds, cancellationToken);
            var systemNameTask = snmpService.WalkAsync(device.IpAddress, community, SnmpOids.Lldp.RemoteSystemName, request.TimeoutMilliseconds, cancellationToken);
            var managementAddressTask = snmpService.WalkAsync(device.IpAddress, community, SnmpOids.Lldp.RemoteManagementAddress, request.TimeoutMilliseconds, cancellationToken);
            await Task.WhenAll(localPortsTask, chassisTask, remotePortTask, remotePortDescriptionTask, systemNameTask, managementAddressTask);

            return new DeviceDiscovery(
                device,
                BuildNeighbors(
                    localPortsTask.Result.Results,
                    chassisTask.Result.Results,
                    remotePortTask.Result.Results,
                    remotePortDescriptionTask.Result.Results,
                    systemNameTask.Result.Results,
                    managementAddressTask.Result.Results),
                null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (SnmpOperationException exception)
        {
            var message = exception.Kind == SnmpErrorKind.Timeout
                ? $"{device.Name} timed out while reading LLDP data."
                : $"{device.Name} could not be queried for LLDP data.";
            return new DeviceDiscovery(device, [], message);
        }
    }

    private static IReadOnlyList<LldpNeighbor> BuildNeighbors(
        IReadOnlyList<SnmpValueResponse> localPorts,
        IReadOnlyList<SnmpValueResponse> chassisIds,
        IReadOnlyList<SnmpValueResponse> remotePortIds,
        IReadOnlyList<SnmpValueResponse> remotePortDescriptions,
        IReadOnlyList<SnmpValueResponse> systemNames,
        IReadOnlyList<SnmpValueResponse> managementAddresses)
    {
        var localPortMap = localPorts
            .Select(value => (Index: LastOidPart(value.Oid), Value: value.Value))
            .Where(item => item.Index is not null && !string.IsNullOrWhiteSpace(item.Value))
            .ToDictionary(item => item.Index!.Value, item => item.Value!, EqualityComparer<int>.Default);
        var rows = new Dictionary<string, LldpNeighborBuilder>(StringComparer.Ordinal);
        ApplyRows(chassisIds, SnmpOids.Lldp.RemoteChassisId, rows, (row, value) => row.ChassisId = value);
        ApplyRows(remotePortIds, SnmpOids.Lldp.RemotePortId, rows, (row, value) => row.RemotePortId = value);
        ApplyRows(remotePortDescriptions, SnmpOids.Lldp.RemotePortDescription, rows, (row, value) => row.RemotePortDescription = value);
        ApplyRows(systemNames, SnmpOids.Lldp.RemoteSystemName, rows, (row, value) => row.SystemName = value);
        ApplyRows(managementAddresses, SnmpOids.Lldp.RemoteManagementAddress, rows, (row, value) => row.ManagementAddress = ToIpv4Address(value));
        foreach (var value in managementAddresses)
        {
            if (!TryGetRemoteIndex(SnmpOids.Lldp.RemoteManagementAddress, value.Oid, out var index)
                || !rows.TryGetValue(index.Key, out var row)
                || row.ManagementAddress is not null)
            {
                continue;
            }

            row.ManagementAddress = TryGetIpv4AddressFromManagementOid(value.Oid);
        }

        return rows.Values
            .Select(row => row.ToNeighbor(localPortMap))
            .Where(neighbor => neighbor is not null)
            .Select(neighbor => neighbor!)
            .ToList();
    }

    private static void ApplyRows(
        IReadOnlyList<SnmpValueResponse> values,
        string rootOid,
        IDictionary<string, LldpNeighborBuilder> rows,
        Action<LldpNeighborBuilder, string?> apply)
    {
        foreach (var value in values)
        {
            if (!TryGetRemoteIndex(rootOid, value.Oid, out var index)) continue;
            if (!rows.TryGetValue(index.Key, out var row))
            {
                row = new LldpNeighborBuilder(index.LocalPortNumber);
                rows[index.Key] = row;
            }

            apply(row, value.Value);
        }
    }

    private static TopologyNodeResponse CreateNeighborNode(
        Device source,
        LldpNeighbor neighbor,
        IReadOnlyDictionary<string, Device> managedDevicesByIp)
    {
        if (neighbor.ManagementIpAddress is not null
            && managedDevicesByIp.TryGetValue(neighbor.ManagementIpAddress, out var managed))
        {
            return ToManagedNode(managed);
        }

        var identity = neighbor.ManagementIpAddress ?? neighbor.ChassisId ?? neighbor.SystemName ?? neighbor.RemotePort ?? "unknown";
        return new TopologyNodeResponse(
            $"discovered:{source.Id}:{identity}",
            null,
            neighbor.ManagementIpAddress,
            neighbor.SystemName ?? neighbor.ChassisId ?? "Discovered LLDP neighbor",
            null,
            false);
    }

    private static TopologyNodeResponse ToManagedNode(Device device) => new(
        $"device:{device.Id}", device.Id, device.IpAddress, device.Name, device.Status, true);

    private static string CreateNormalizedEdgeKey(TopologyEdgeResponse edge)
    {
        var forward = string.Join('|', edge.SourceNodeId, edge.TargetNodeId, edge.LocalPort ?? string.Empty, edge.RemotePort ?? string.Empty);
        var reverse = string.Join('|', edge.TargetNodeId, edge.SourceNodeId, edge.RemotePort ?? string.Empty, edge.LocalPort ?? string.Empty);
        return string.CompareOrdinal(forward, reverse) <= 0 ? forward : reverse;
    }

    private void ValidateRequest(TopologyDiscoveryRequest request)
    {
        if (request.DeviceIds.Count == 0) throw new TopologyDiscoveryValidationException("Select at least one device.");
        if (request.DeviceIds.Any(id => id <= 0)) throw new TopologyDiscoveryValidationException("Device IDs must be positive.");
        if (request.DeviceIds.Distinct().Count() != request.DeviceIds.Count) throw new TopologyDiscoveryValidationException("Selected devices must be unique.");
        if (request.DeviceIds.Count > _options.MaxDevicesPerDiscovery) throw new TopologyDiscoveryValidationException($"A maximum of {_options.MaxDevicesPerDiscovery} devices can be discovered at once.");
        if (string.IsNullOrWhiteSpace(request.Community)) throw new TopologyDiscoveryValidationException("SNMP community is required.");
        if (request.TimeoutMilliseconds is < SnmpServiceTimeouts.MinimumMilliseconds or > SnmpServiceTimeouts.MaximumMilliseconds)
            throw new TopologyDiscoveryValidationException($"Timeout must be between {SnmpServiceTimeouts.MinimumMilliseconds} and {SnmpServiceTimeouts.MaximumMilliseconds} milliseconds.");
    }

    private static bool TryGetRemoteIndex(string rootOid, string oid, out (string Key, int LocalPortNumber) index)
    {
        index = default;
        var suffix = oid.StartsWith(rootOid + ".", StringComparison.Ordinal) ? oid[(rootOid.Length + 1)..].Split('.') : [];
        if (suffix.Length < 3 || !int.TryParse(suffix[1], out var localPortNumber)) return false;
        index = (string.Join('.', suffix.Take(3)), localPortNumber);
        return true;
    }

    private static int? LastOidPart(string oid) => int.TryParse(oid[(oid.LastIndexOf('.') + 1)..], out var index) ? index : null;

    private static string? ToIpv4Address(string? value)
    {
        if (IPAddress.TryParse(value, out var parsed) && parsed.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) return parsed.ToString();
        return null;
    }

    private static string? TryGetIpv4AddressFromManagementOid(string oid)
    {
        var parts = oid.Split('.');
        if (parts.Length < 10 || !int.TryParse(parts[^6], out var subtype) || subtype != 1 || !int.TryParse(parts[^5], out var length) || length != 4)
        {
            return null;
        }

        var addressBytes = parts.TakeLast(4).Select(part => byte.TryParse(part, out var value) ? value : (byte?)null).ToArray();
        return addressBytes.All(value => value.HasValue) ? new IPAddress(addressBytes.Select(value => value!.Value).ToArray()).ToString() : null;
    }

    private sealed record DeviceDiscovery(Device Device, IReadOnlyList<LldpNeighbor> Neighbors, string? Warning);

    private sealed record LldpNeighbor(string? ChassisId, string? SystemName, string? ManagementIpAddress, string? LocalPort, string? RemotePort);

    private sealed class LldpNeighborBuilder(int localPortNumber)
    {
        public string? ChassisId { get; set; }
        public string? RemotePortId { get; set; }
        public string? RemotePortDescription { get; set; }
        public string? SystemName { get; set; }
        public string? ManagementAddress { get; set; }

        public LldpNeighbor? ToNeighbor(IReadOnlyDictionary<int, string> localPortMap)
        {
            var remotePort = RemotePortDescription ?? RemotePortId;
            if (ChassisId is null && SystemName is null && ManagementAddress is null && remotePort is null) return null;
            return new LldpNeighbor(ChassisId, SystemName, ManagementAddress, localPortMap.GetValueOrDefault(localPortNumber) ?? localPortNumber.ToString(), remotePort);
        }
    }
}
