using NetworkMonitor.Api.Dtos;

namespace NetworkMonitor.Api.Services;

public sealed class CiscoIosConfigBackupProvider : IConfigBackupProvider
{
    private static readonly IReadOnlyList<string> Commands = ["show running-config"];

    public ConfigBackupVendor Vendor => ConfigBackupVendor.CiscoIos;

    public IReadOnlyList<string> GetRunningConfigurationCommands() => Commands;
}
