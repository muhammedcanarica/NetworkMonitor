namespace NetworkMonitor.Api.Configuration;

public sealed class ConfigBackupOptions
{
    public const string SectionName = "ConfigBackup";

    public int ConnectionTimeoutMilliseconds { get; set; } = 10000;

    public int CommandTimeoutMilliseconds { get; set; } = 30000;
}
