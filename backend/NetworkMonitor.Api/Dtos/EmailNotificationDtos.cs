using System.ComponentModel.DataAnnotations;
using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Dtos;

public sealed record UpdateEmailNotificationSettingsRequest
{
    public bool IsEnabled { get; init; }
    [StringLength(255)] public string Host { get; init; } = string.Empty;
    [Range(1, 65535)] public int Port { get; init; } = 587;
    public EmailTlsMode TlsMode { get; init; } = EmailTlsMode.StartTls;
    [StringLength(255)] public string? Username { get; init; }
    [StringLength(1024)] public string? Password { get; init; }
    [StringLength(320)] public string FromAddress { get; init; } = string.Empty;
    [StringLength(100)] public string? FromName { get; init; }
    [MaxLength(50)] public IReadOnlyList<string> RecipientAddresses { get; init; } = [];
    public override string ToString() => $"Update email notifications for {Host}:{Port}, password {(string.IsNullOrWhiteSpace(Password) ? "unchanged" : "[REDACTED]")}";
}

public sealed record EmailNotificationSettingsResponse(
    bool IsEnabled,
    string Host,
    int Port,
    EmailTlsMode TlsMode,
    string? Username,
    string FromAddress,
    string? FromName,
    IReadOnlyList<string> RecipientAddresses,
    bool HasPassword,
    DateTimeOffset? UpdatedAt);

public sealed record TestEmailResponse(string Message);
