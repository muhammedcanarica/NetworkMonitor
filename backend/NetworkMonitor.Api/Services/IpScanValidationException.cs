namespace NetworkMonitor.Api.Services;

public sealed class IpScanValidationException(string message) : ArgumentException(message);
