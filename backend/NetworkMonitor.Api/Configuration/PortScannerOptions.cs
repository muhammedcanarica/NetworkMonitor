namespace NetworkMonitor.Api.Configuration;

public sealed class PortScannerOptions
{
    public const string SectionName = "PortScanner";

    public int MaxPortsPerScan { get; set; } = 256;

    public int MaxConcurrentConnections { get; set; } = 32;

    public int MinimumTimeoutMilliseconds { get; set; } = 100;

    public int MaximumTimeoutMilliseconds { get; set; } = 10000;
}
