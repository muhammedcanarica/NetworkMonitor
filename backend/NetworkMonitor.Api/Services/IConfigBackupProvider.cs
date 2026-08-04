using NetworkMonitor.Api.Dtos;

namespace NetworkMonitor.Api.Services;

public interface IConfigBackupProvider
{
    ConfigBackupVendor Vendor { get; }

    IReadOnlyList<string> GetRunningConfigurationCommands();
}
