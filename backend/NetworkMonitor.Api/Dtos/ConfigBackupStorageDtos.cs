namespace NetworkMonitor.Api.Dtos;

public sealed class SaveConfigBackupRequest
{
    public int? DeviceId { get; init; }

    public string IpAddress { get; init; } = string.Empty;

    public ConfigBackupVendor Vendor { get; init; } = ConfigBackupVendor.CiscoIos;

    public string Configuration { get; init; } = string.Empty;

    public DateTimeOffset CapturedAt { get; init; }
}

public sealed record ConfigBackupListItemResponse(
    int Id,
    int? DeviceId,
    string IpAddress,
    ConfigBackupVendor Vendor,
    DateTimeOffset CapturedAt,
    DateTimeOffset CreatedAt,
    string Hash,
    int ConfigurationLength);

public sealed record ConfigBackupDetailResponse(
    int Id,
    int? DeviceId,
    string IpAddress,
    ConfigBackupVendor Vendor,
    string Configuration,
    DateTimeOffset CapturedAt,
    DateTimeOffset CreatedAt,
    string Hash);

public sealed record SaveConfigBackupResponse(
    bool ConfigurationChanged,
    int BackupId,
    int? ExistingBackupId,
    ConfigBackupListItemResponse Backup);

public enum ConfigDiffLineType
{
    Added,
    Removed,
    Unchanged
}

public sealed record ConfigDiffLineResponse(
    ConfigDiffLineType Type,
    int? FromLineNumber,
    int? ToLineNumber,
    string Content);

public sealed record ConfigBackupComparisonResponse(
    ConfigBackupListItemResponse FromBackup,
    ConfigBackupListItemResponse ToBackup,
    int AddedLines,
    int RemovedLines,
    bool Changed,
    IReadOnlyList<ConfigDiffLineResponse> DiffLines);
