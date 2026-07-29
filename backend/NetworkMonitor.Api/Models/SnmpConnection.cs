using System.Text.Json.Serialization;

namespace NetworkMonitor.Api.Models;

public sealed class SnmpConnection(
    string ipAddress,
    string community,
    int timeoutMilliseconds)
{
    public string IpAddress { get; } = ipAddress;

    [JsonIgnore]
    public string Community { get; } = community;

    public int TimeoutMilliseconds { get; } = timeoutMilliseconds;

    public override string ToString()
    {
        return $"SNMP v2c target {IpAddress}, community [REDACTED], timeout {TimeoutMilliseconds} ms";
    }
}
