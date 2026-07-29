namespace NetworkMonitor.Api.Services;

public enum SnmpErrorKind
{
    Timeout,
    Unavailable,
    UnsupportedResponse,
    Unknown
}

public sealed class SnmpValidationException(string message) : ArgumentException(message);

public sealed class SnmpOperationException(
    SnmpErrorKind kind,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    public SnmpErrorKind Kind { get; } = kind;
}
