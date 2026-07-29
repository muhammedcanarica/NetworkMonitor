namespace NetworkMonitor.Api.Configuration;

public sealed class IpScannerOptions
{
    public const string SectionName = "IpScanner";

    public int PingTimeoutMilliseconds { get; set; } = 1000;

    public int MaxConcurrentPings { get; set; } = 64;

    public int MaxAddressesPerScan { get; set; } = 1024;

    public int HostNameTimeoutMilliseconds { get; set; } = 500;
}
