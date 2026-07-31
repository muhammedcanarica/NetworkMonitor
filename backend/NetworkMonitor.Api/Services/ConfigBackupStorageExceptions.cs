namespace NetworkMonitor.Api.Services;

public sealed class ConfigBackupStorageValidationException(string message) : ArgumentException(message);

public sealed class ConfigBackupNotFoundException(int id) : Exception($"Configuration backup {id} was not found.");

public sealed class ConfigBackupSizeLimitException(string message) : Exception(message);
