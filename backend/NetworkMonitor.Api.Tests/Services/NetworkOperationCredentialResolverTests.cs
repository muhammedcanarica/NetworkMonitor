using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Cryptography;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Models;
using NetworkMonitor.Api.Services;

namespace NetworkMonitor.Api.Tests.Services;

public sealed class NetworkOperationCredentialResolverTests
{
    [Fact]
    public async Task ResolveSnmpCommunityAsync_ReturnsTrimmedManualValue()
    {
        var resolver = CreateResolver();

        var community = await resolver.ResolveSnmpCommunityAsync("  private  ", null, CancellationToken.None);

        Assert.Equal("private", community);
    }

    [Fact]
    public async Task ResolveSnmpCommunityAsync_RejectsManualAndSavedSourcesTogether()
    {
        var resolver = CreateResolver();

        var exception = await Assert.ThrowsAsync<NetworkOperationCredentialException>(() =>
            resolver.ResolveSnmpCommunityAsync("private", 7, CancellationToken.None));

        Assert.DoesNotContain("private", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveSnmpCommunityAsync_UsesSavedSnmpCredential()
    {
        var resolver = CreateResolver((id, _) =>
            Task.FromResult((NetworkCredentialType.SnmpV2Community, (string?)null, "stored-community")));

        var community = await resolver.ResolveSnmpCommunityAsync(null, 7, CancellationToken.None);

        Assert.Equal("stored-community", community);
    }

    [Fact]
    public async Task ResolveSnmpCommunityAsync_RejectsWrongCredentialType()
    {
        var resolver = CreateResolver((id, _) =>
            Task.FromResult((NetworkCredentialType.SshPassword, (string?)"operator", "password")));

        var exception = await Assert.ThrowsAsync<NetworkOperationCredentialException>(() =>
            resolver.ResolveSnmpCommunityAsync(null, 7, CancellationToken.None));

        Assert.Contains("not an SNMP", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("password", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveSshCredentialAsync_UsesUsernameAndPasswordFromSavedCredential()
    {
        var resolver = CreateResolver((id, _) =>
            Task.FromResult((NetworkCredentialType.SshPassword, (string?)"operator", "stored-password")));

        var credential = await resolver.ResolveSshCredentialAsync(null, null, 12, CancellationToken.None);

        Assert.Equal("operator", credential.Username);
        Assert.Equal("stored-password", credential.Password);
    }

    [Fact]
    public async Task ResolveSshCredentialAsync_ReturnsManualUsernameAndPassword()
    {
        var resolver = CreateResolver();

        var credential = await resolver.ResolveSshCredentialAsync(" operator ", "manual-password", null, CancellationToken.None);

        Assert.Equal("operator", credential.Username);
        Assert.Equal("manual-password", credential.Password);
    }

    [Fact]
    public async Task ResolveSshCredentialAsync_RejectsWrongCredentialType()
    {
        var resolver = CreateResolver((id, _) =>
            Task.FromResult((NetworkCredentialType.SnmpV2Community, (string?)null, "community")));

        var exception = await Assert.ThrowsAsync<NetworkOperationCredentialException>(() =>
            resolver.ResolveSshCredentialAsync(null, null, 12, CancellationToken.None));

        Assert.Contains("not an SSH", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("community", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveSshCredentialAsync_RejectsManualAndSavedSourcesTogether()
    {
        var resolver = CreateResolver();

        await Assert.ThrowsAsync<NetworkOperationCredentialException>(() =>
            resolver.ResolveSshCredentialAsync("operator", "manual-password", 12, CancellationToken.None));
    }

    [Fact]
    public async Task ResolveSavedCredentialAsync_MapsMissingCredentialToSafeMessage()
    {
        var resolver = CreateResolver((_, _) => throw new KeyNotFoundException("database details"));

        var exception = await Assert.ThrowsAsync<NetworkOperationCredentialException>(() =>
            resolver.ResolveSnmpCommunityAsync(null, 404, CancellationToken.None));

        Assert.Equal("The selected credential could not be found or decrypted.", exception.Message);
    }

    [Fact]
    public async Task ResolveSavedCredentialAsync_MapsLookupOrDecryptionFailureToSafeMessage()
    {
        var resolver = CreateResolver((_, _) => throw new CryptographicException("raw key-ring and cipher details"));

        var exception = await Assert.ThrowsAsync<NetworkOperationCredentialException>(() =>
            resolver.ResolveSnmpCommunityAsync(null, 7, CancellationToken.None));

        Assert.Equal("The selected credential could not be found or decrypted.", exception.Message);
        Assert.DoesNotContain("key-ring", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveSavedCredentialAsync_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var resolver = CreateResolver((_, token) => Task.FromCanceled<(NetworkCredentialType, string?, string)>(token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            resolver.ResolveSnmpCommunityAsync(null, 7, cancellation.Token));
    }

    private static NetworkOperationCredentialResolver CreateResolver(
        Func<int, CancellationToken, Task<(NetworkCredentialType Type, string? Username, string Secret)>>? resolve = null)
    {
        return new NetworkOperationCredentialResolver(
            new StubNetworkCredentialService(resolve),
            NullLogger<NetworkOperationCredentialResolver>.Instance);
    }

    private sealed class StubNetworkCredentialService(
        Func<int, CancellationToken, Task<(NetworkCredentialType Type, string? Username, string Secret)>>? resolve) : INetworkCredentialService
    {
        public Task<(NetworkCredentialType Type, string? Username, string Secret)> ResolveAsync(int id, CancellationToken cancellationToken)
            => resolve?.Invoke(id, cancellationToken)
                ?? throw new NotSupportedException();

        public Task<NetworkCredentialResponse> CreateAsync(CreateNetworkCredentialRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<NetworkCredentialResponse>> ListAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<NetworkCredentialResponse> UpdateAsync(int id, UpdateNetworkCredentialRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteAsync(int id, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
