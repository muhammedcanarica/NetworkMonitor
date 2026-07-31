namespace NetworkMonitor.Api.Services;

public sealed class WakeOnLanValidationException(string message) : ArgumentException(message);

public sealed class WakeOnLanOperationException(string message, Exception innerException)
    : Exception(message, innerException);
