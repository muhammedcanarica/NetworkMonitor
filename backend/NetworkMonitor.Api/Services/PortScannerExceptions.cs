namespace NetworkMonitor.Api.Services;

public sealed class PortScanValidationException(string message) : ArgumentException(message);

public sealed class PortScanOperationException(string message, Exception innerException)
    : Exception(message, innerException);
