namespace NetworkMonitor.Api.Configuration;

public sealed class ConfigBackupOptions
{
    public const string SectionName = "ConfigBackup";

    public int ConnectionTimeoutMilliseconds { get; set; } = 10000;

    public int CommandTimeoutMilliseconds { get; set; } = 30000;

    public int MaxStoredConfigurationLength { get; set; } = 1_048_576;

    public int MaxDiffLines { get; set; } = 2_000;
}
