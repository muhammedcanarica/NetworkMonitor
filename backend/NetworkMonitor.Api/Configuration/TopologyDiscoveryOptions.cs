namespace NetworkMonitor.Api.Configuration;

public sealed class TopologyDiscoveryOptions
{
    public const string SectionName = "TopologyDiscovery";

    public int MaxDevicesPerDiscovery { get; init; } = 32;

    public int MaxConcurrentDiscoveries { get; init; } = 4;
}
