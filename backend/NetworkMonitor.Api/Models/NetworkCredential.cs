namespace NetworkMonitor.Api.Models;

public sealed class NetworkCredential
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public NetworkCredentialType Type { get; set; }
    public string? Username { get; set; }
    public string ProtectedSecret { get; set; } = string.Empty;
    public int? DeviceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Device? Device { get; set; }

    public override string ToString() => $"Network credential {Id} ({Name}, {Type}), secret [PROTECTED]";
}

public enum NetworkCredentialType { SnmpV2Community, SshPassword }
