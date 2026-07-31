using NetworkMonitor.Api.Services;

namespace NetworkMonitor.Api.Tests.Infrastructure;

public sealed class StubNetworkOperationCredentialResolver : INetworkOperationCredentialResolver
{
    public Func<string?, int?, CancellationToken, Task<string>> SnmpHandler { get; init; }
        = (community, _, _) => Task.FromResult(community ?? "resolved-community");

    public Func<string?, string?, int?, CancellationToken, Task<SshCredential>> SshHandler { get; init; }
        = (username, password, _, _) => Task.FromResult(new SshCredential(username ?? "resolved-user", password ?? "resolved-password"));

    public Task<string> ResolveSnmpCommunityAsync(string? community, int? credentialId, CancellationToken cancellationToken)
        => SnmpHandler(community, credentialId, cancellationToken);

    public Task<SshCredential> ResolveSshCredentialAsync(string? username, string? password, int? credentialId, CancellationToken cancellationToken)
        => SshHandler(username, password, credentialId, cancellationToken);
}
