using System.ComponentModel.DataAnnotations;

namespace NetworkMonitor.Api.Dtos;

public abstract class SnmpRequestBase
{
    [Required(AllowEmptyStrings = false)]
    [ValidIpAddress]
    public string IpAddress { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [StringLength(255, MinimumLength = 1)]
    public string Community { get; init; } = string.Empty;

    [Range(500, 10000)]
    public int TimeoutMilliseconds { get; init; } = 2000;

    public override string ToString()
    {
        return $"{GetType().Name} for {IpAddress}, community [REDACTED], timeout {TimeoutMilliseconds} ms";
    }
}

public sealed class SnmpSystemInfoRequest : SnmpRequestBase;

public sealed class SnmpInterfacesRequest : SnmpRequestBase;

public sealed class SnmpGetRequest : SnmpRequestBase
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(512)]
    public string Oid { get; init; } = string.Empty;
}

public sealed class SnmpWalkRequest : SnmpRequestBase
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(512)]
    public string RootOid { get; init; } = string.Empty;
}
