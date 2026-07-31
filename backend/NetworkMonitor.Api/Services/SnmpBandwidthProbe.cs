using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Services;

public sealed class SnmpBandwidthProbe(ISnmpTransport transport) : ISnmpBandwidthProbe
{
    private const int InterfacesPerBatch = 16;

    public async Task<IReadOnlyList<InterfaceCounterReading>> ReadAsync(
        string ipAddress,
        string community,
        IReadOnlyList<int> interfaceIndexes,
        int timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        var connection = new SnmpConnection(ipAddress, community, timeoutMilliseconds);
        var uptimeValues = await transport.GetAsync(connection, [SnmpOids.System.UpTime], cancellationToken);
        var uptime = ToInt64(uptimeValues.FirstOrDefault(item => item.Oid == SnmpOids.System.UpTime)?.NumericValue)
            ?? throw new SnmpOperationException(SnmpErrorKind.UnsupportedResponse, "The SNMP agent did not return sysUpTime.");
        var results = new List<InterfaceCounterReading>(interfaceIndexes.Count);

        foreach (var batch in interfaceIndexes.Chunk(InterfacesPerBatch))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var oids = batch.SelectMany(CreateInterfaceOids).ToArray();
            var values = await transport.GetAsync(connection, oids, cancellationToken);
            var byOid = values.GroupBy(item => item.Oid, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            foreach (var index in batch)
            {
                var inbound = GetValue(byOid, SnmpOids.Interfaces.HighCapacityInOctets, index);
                var outbound = GetValue(byOid, SnmpOids.Interfaces.HighCapacityOutOctets, index);
                if (!string.Equals(inbound?.Type, "Counter64", StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(outbound?.Type, "Counter64", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var inOctets = ToInt64(inbound?.NumericValue);
                var outOctets = ToInt64(outbound?.NumericValue);
                if (!inOctets.HasValue || !outOctets.HasValue)
                {
                    continue;
                }

                var operStatus = GetValue(byOid, SnmpOids.Interfaces.OperStatus, index)?.NumericValue switch
                {
                    1 => "Up",
                    2 => "Down",
                    3 => "Testing",
                    4 => "Unknown",
                    5 => "Dormant",
                    6 => "NotPresent",
                    7 => "LowerLayerDown",
                    _ => null
                };
                var adminStatus = GetValue(byOid, SnmpOids.Interfaces.AdminStatus, index)?.NumericValue switch
                {
                    1 => "Up",
                    2 => "Down",
                    3 => "Testing",
                    _ => null
                };
                var discontinuity = ToInt64(GetValue(byOid, SnmpOids.Interfaces.CounterDiscontinuityTime, index)?.NumericValue);
                results.Add(new InterfaceCounterReading(index, inOctets.Value, outOctets.Value, adminStatus, operStatus, uptime, discontinuity));
            }
        }

        return results;
    }

    private static IEnumerable<string> CreateInterfaceOids(int index)
    {
        yield return $"{SnmpOids.Interfaces.HighCapacityInOctets}.{index}";
        yield return $"{SnmpOids.Interfaces.HighCapacityOutOctets}.{index}";
        yield return $"{SnmpOids.Interfaces.OperStatus}.{index}";
        yield return $"{SnmpOids.Interfaces.AdminStatus}.{index}";
        yield return $"{SnmpOids.Interfaces.CounterDiscontinuityTime}.{index}";
    }

    private static SnmpVariableValue? GetValue(IReadOnlyDictionary<string, SnmpVariableValue> values, string rootOid, int index)
        => values.GetValueOrDefault($"{rootOid}.{index}");

    private static long? ToInt64(ulong? value) => value is <= long.MaxValue ? (long)value.Value : null;
}
