namespace NetworkMonitor.Api.Dtos;

public enum ConfigBackupVendor
{
    CiscoIos
}

public sealed class ConfigBackupRequest
{
    public string IpAddress { get; init; } = string.Empty;

    public int Port { get; init; } = 22;

    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public ConfigBackupVendor Vendor { get; init; } = ConfigBackupVendor.CiscoIos;

    public override string ToString()
    {
        return $"ConfigBackupRequest {{ IpAddress = {IpAddress}, Port = {Port}, Username = [REDACTED], Password = [REDACTED], Vendor = {Vendor} }}";
    }
}

public sealed record ConfigBackupResponse(
    string IpAddress,
    ConfigBackupVendor Vendor,
    string Configuration,
    DateTimeOffset CapturedAt,
    string SuggestedFileName);
