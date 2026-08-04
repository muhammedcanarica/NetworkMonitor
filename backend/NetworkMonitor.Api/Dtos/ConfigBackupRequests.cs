namespace NetworkMonitor.Api.Dtos;

public enum ConfigBackupVendor
{
    CiscoIos,
    Fortinet
}

public sealed class ConfigBackupRequest
{
    public string IpAddress { get; init; } = string.Empty;

    public int Port { get; init; } = 22;

    public string? Username { get; init; }

    public string? Password { get; init; }

    public int? CredentialId { get; init; }

    public ConfigBackupVendor Vendor { get; init; } = ConfigBackupVendor.CiscoIos;

    public override string ToString()
    {
        return $"ConfigBackupRequest {{ IpAddress = {IpAddress}, Port = {Port}, CredentialSource = {(CredentialId.HasValue ? "Saved" : "Manual")}, Vendor = {Vendor} }}";
    }
}

public sealed record ConfigBackupResponse(
    string IpAddress,
    ConfigBackupVendor Vendor,
    string Configuration,
    DateTimeOffset CapturedAt,
    string SuggestedFileName);
