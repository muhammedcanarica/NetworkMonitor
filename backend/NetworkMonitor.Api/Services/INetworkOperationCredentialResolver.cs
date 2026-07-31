namespace NetworkMonitor.Api.Services;

public interface INetworkOperationCredentialResolver
{
    Task<string> ResolveSnmpCommunityAsync(
        string? community,
        int? credentialId,
        CancellationToken cancellationToken);

    Task<SshCredential> ResolveSshCredentialAsync(
        string? username,
        string? password,
        int? credentialId,
        CancellationToken cancellationToken);
}

public sealed record SshCredential(string Username, string Password);

public sealed class NetworkOperationCredentialException(string message) : ArgumentException(message);
