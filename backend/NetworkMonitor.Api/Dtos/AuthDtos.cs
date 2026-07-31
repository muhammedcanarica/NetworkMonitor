using System.ComponentModel.DataAnnotations;

namespace NetworkMonitor.Api.Dtos;

public sealed class LoginRequest
{
    [Required, StringLength(100)] public string Username { get; init; } = string.Empty;
    [Required, StringLength(256)] public string Password { get; init; } = string.Empty;
    public override string ToString() => $"Login request for {Username}, password [REDACTED]";
}

public sealed record CurrentUserResponse(string Username);
