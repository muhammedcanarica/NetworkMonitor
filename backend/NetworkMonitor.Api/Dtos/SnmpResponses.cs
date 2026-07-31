namespace NetworkMonitor.Api.Dtos;

public sealed record SnmpValueResponse(
    string Oid,
    string? Value,
    string Type);

public sealed record SnmpWalkResponse(
    string RootOid,
    int Count,
    IReadOnlyList<SnmpValueResponse> Results);

public sealed record SnmpSystemInfoResponse(
    string IpAddress,
    string? SysName,
    string? SysDescription,
    string? SysObjectId,
    ulong? SysUpTimeTicks,
    string? SysContact,
    string? SysLocation);

public sealed record SnmpInterfaceResponse(
    int Index,
    string? Name,
    string? Description,
    string AdminStatus,
    string OperStatus,
    ulong? SpeedBitsPerSecond);
