namespace NetworkMonitor.Api.Services;

public enum ConfigBackupErrorKind
{
    Authentication,
    Connection,
    ConnectionTimeout,
    CommandTimeout,
    Unexpected
}

public sealed class ConfigBackupValidationException(string message) : ArgumentException(message);

public sealed class ConfigBackupOperationException(
    ConfigBackupErrorKind kind,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    public ConfigBackupErrorKind Kind { get; } = kind;
}

public sealed class SshCommandTransportException(
    ConfigBackupErrorKind kind,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    public ConfigBackupErrorKind Kind { get; } = kind;
}
