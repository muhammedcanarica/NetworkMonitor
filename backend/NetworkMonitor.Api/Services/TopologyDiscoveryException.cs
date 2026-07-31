namespace NetworkMonitor.Api.Services;

public sealed class TopologyDiscoveryValidationException(string message) : ArgumentException(message);
