using System.Net;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Services;

public sealed class SnmpService(ISnmpTransport transport) : ISnmpService
{
    public const int MaxWalkResults = 500;
    public const int MinimumTimeoutMilliseconds = SnmpServiceTimeouts.MinimumMilliseconds;
    public const int MaximumTimeoutMilliseconds = SnmpServiceTimeouts.MaximumMilliseconds;

    public async Task<SnmpValueResponse> GetAsync(
        string ipAddress,
        string community,
        string oid,
        int timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        var connection = CreateConnection(ipAddress, community, timeoutMilliseconds);
        var normalizedOid = NormalizeOid(oid);
        var values = await transport.GetAsync(connection, [normalizedOid], cancellationToken);
        var value = values.FirstOrDefault(item => item.Oid == normalizedOid)
            ?? values.FirstOrDefault()
            ?? throw new SnmpOperationException(
                SnmpErrorKind.UnsupportedResponse,
                "The SNMP agent returned an empty response.");

        return ToResponse(value);
    }

    public async Task<SnmpWalkResponse> WalkAsync(
        string ipAddress,
        string community,
        string rootOid,
        int timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        var connection = CreateConnection(ipAddress, community, timeoutMilliseconds);
        var normalizedRootOid = NormalizeOid(rootOid);
        var values = await transport.WalkAsync(
            connection,
            normalizedRootOid,
            MaxWalkResults,
            cancellationToken);
        var results = values.Take(MaxWalkResults).Select(ToResponse).ToList();

        return new SnmpWalkResponse(normalizedRootOid, results.Count, results);
    }

    public async Task<SnmpSystemInfoResponse> GetSystemInfoAsync(
        string ipAddress,
        string community,
        int timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        var connection = CreateConnection(ipAddress, community, timeoutMilliseconds);
        var values = await transport.GetAsync(connection, SnmpOids.System.All, cancellationToken);
        var byOid = values
            .GroupBy(item => item.Oid, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        return new SnmpSystemInfoResponse(
            connection.IpAddress,
            GetValue(byOid, SnmpOids.System.Name)?.Value,
            GetValue(byOid, SnmpOids.System.Description)?.Value,
            GetValue(byOid, SnmpOids.System.ObjectId)?.Value,
            GetValue(byOid, SnmpOids.System.UpTime)?.NumericValue,
            GetValue(byOid, SnmpOids.System.Contact)?.Value,
            GetValue(byOid, SnmpOids.System.Location)?.Value);
    }

    public async Task<IReadOnlyList<SnmpInterfaceResponse>> GetInterfacesAsync(
        string ipAddress,
        string community,
        int timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        var connection = CreateConnection(ipAddress, community, timeoutMilliseconds);
        var columns = new[]
        {
            SnmpOids.Interfaces.Index,
            SnmpOids.Interfaces.Name,
            SnmpOids.Interfaces.Description,
            SnmpOids.Interfaces.Speed,
            SnmpOids.Interfaces.AdminStatus,
            SnmpOids.Interfaces.OperStatus
        };
        var rows = new Dictionary<int, InterfaceBuilder>();

        foreach (var columnOid in columns)
        {
            var values = await transport.WalkAsync(
                connection,
                columnOid,
                MaxWalkResults,
                cancellationToken);
            foreach (var value in values)
            {
                if (!TryGetInterfaceIndex(columnOid, value.Oid, out var index))
                {
                    continue;
                }

                if (!rows.TryGetValue(index, out var row))
                {
                    row = new InterfaceBuilder(index);
                    rows[index] = row;
                }

                row.Apply(columnOid, value);
            }
        }

        return rows.Values
            .OrderBy(row => row.Index)
            .Select(row => row.ToResponse())
            .ToList();
    }

    private static SnmpConnection CreateConnection(
        string ipAddress,
        string community,
        int timeoutMilliseconds)
    {
        if (!IPAddress.TryParse(ipAddress?.Trim(), out var address))
        {
            throw new SnmpValidationException("The target must be a valid IPv4 or IPv6 address.");
        }

        if (string.IsNullOrWhiteSpace(community))
        {
            throw new SnmpValidationException("Community is required.");
        }

        if (timeoutMilliseconds is < MinimumTimeoutMilliseconds or > MaximumTimeoutMilliseconds)
        {
            throw new SnmpValidationException(
                $"Timeout must be between {MinimumTimeoutMilliseconds} and {MaximumTimeoutMilliseconds} milliseconds.");
        }

        return new SnmpConnection(address.ToString(), community, timeoutMilliseconds);
    }

    private static string NormalizeOid(string oid)
    {
        if (!SnmpOid.TryNormalize(oid, out var normalized))
        {
            throw new SnmpValidationException("The OID format is invalid.");
        }

        return normalized;
    }

    private static SnmpValueResponse ToResponse(SnmpVariableValue value)
    {
        return new SnmpValueResponse(value.Oid, value.Value, value.Type);
    }

    private static SnmpVariableValue? GetValue(
        IReadOnlyDictionary<string, SnmpVariableValue> values,
        string oid)
    {
        return values.GetValueOrDefault(oid);
    }

    private static bool TryGetInterfaceIndex(string columnOid, string oid, out int index)
    {
        index = 0;
        var prefix = columnOid + ".";
        return oid.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(oid[prefix.Length..], out index)
            && index > 0;
    }

    private sealed class InterfaceBuilder(int index)
    {
        public int Index { get; } = index;

        private string? Description { get; set; }

        private string? Name { get; set; }

        private string AdminStatus { get; set; } = "Unknown";

        private string OperStatus { get; set; } = "Unknown";

        private ulong? SpeedBitsPerSecond { get; set; }

        public void Apply(string columnOid, SnmpVariableValue value)
        {
            switch (columnOid)
            {
                case SnmpOids.Interfaces.Name:
                    Name = value.Value;
                    break;
                case SnmpOids.Interfaces.Description:
                    Description = value.Value;
                    break;
                case SnmpOids.Interfaces.Speed:
                    SpeedBitsPerSecond = value.NumericValue;
                    break;
                case SnmpOids.Interfaces.AdminStatus:
                    AdminStatus = MapStatus(value.NumericValue);
                    break;
                case SnmpOids.Interfaces.OperStatus:
                    OperStatus = MapStatus(value.NumericValue);
                    break;
            }
        }

        public SnmpInterfaceResponse ToResponse()
        {
            return new SnmpInterfaceResponse(
                Index,
                Name,
                Description,
                AdminStatus,
                OperStatus,
                SpeedBitsPerSecond);
        }

        private static string MapStatus(ulong? status)
        {
            return status switch
            {
                1 => "Up",
                2 => "Down",
                3 => "Testing",
                _ => "Unknown"
            };
        }
    }
}
