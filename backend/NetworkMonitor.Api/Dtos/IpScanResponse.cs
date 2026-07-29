namespace NetworkMonitor.Api.Dtos;

public sealed record IpScanResponse(
    string Cidr,
    int ScannedAddresses,
    int ReachableHosts,
    long DurationMs,
    IReadOnlyList<IpScanHostResponse> Results);

public sealed record IpScanHostResponse(
    string IpAddress,
    bool IsReachable,
    long? LatencyMs,
    string? HostName,
    bool IsAlreadyMonitored,
    int? DeviceId);
