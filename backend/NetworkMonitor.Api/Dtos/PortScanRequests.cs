namespace NetworkMonitor.Api.Dtos;

public sealed class PortScanRequest
{
    public string IpAddress { get; init; } = string.Empty;

    public IReadOnlyList<int> Ports { get; init; } = [];

    public int TimeoutMilliseconds { get; init; } = 1000;
}

public enum PortState
{
    Open,
    Closed
}

public sealed record PortScanResult(
    int Port,
    PortState State,
    long? LatencyMs,
    string? ServiceName);

public sealed record PortScanResponse(
    string IpAddress,
    int ScannedPorts,
    int OpenPorts,
    long DurationMs,
    IReadOnlyList<PortScanResult> Results);
