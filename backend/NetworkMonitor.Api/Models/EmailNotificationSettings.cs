namespace NetworkMonitor.Api.Models;

public sealed class EmailNotificationSettings
{
    public int Id { get; set; }
    public bool IsEnabled { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public EmailTlsMode TlsMode { get; set; } = EmailTlsMode.StartTls;
    public string? Username { get; set; }
    public string? ProtectedPassword { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public string? FromName { get; set; }
    public string RecipientAddresses { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public enum EmailTlsMode { None, StartTls, SslOnConnect }
