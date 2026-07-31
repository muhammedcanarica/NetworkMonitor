using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Services;

public sealed class NetworkOperationCredentialResolver(
    INetworkCredentialService credentialService,
    ILogger<NetworkOperationCredentialResolver> logger) : INetworkOperationCredentialResolver
{
    public async Task<string> ResolveSnmpCommunityAsync(
        string? community,
        int? credentialId,
        CancellationToken cancellationToken)
    {
        var hasManualCommunity = !string.IsNullOrWhiteSpace(community);
        ValidateSource(hasManualCommunity, credentialId, "SNMP community");

        if (!credentialId.HasValue)
        {
            return community!.Trim();
        }

        var credential = await ResolveSavedCredentialAsync(credentialId.Value, cancellationToken);
        if (credential.Type != NetworkCredentialType.SnmpV2Community)
        {
            throw new NetworkOperationCredentialException("The selected credential is not an SNMP v2c community.");
        }

        if (string.IsNullOrWhiteSpace(credential.Secret))
        {
            throw new NetworkOperationCredentialException("The selected credential cannot be used.");
        }

        return credential.Secret;
    }

    public async Task<SshCredential> ResolveSshCredentialAsync(
        string? username,
        string? password,
        int? credentialId,
        CancellationToken cancellationToken)
    {
        var hasUsername = !string.IsNullOrWhiteSpace(username);
        var hasPassword = !string.IsNullOrEmpty(password);
        var hasAnyManualValue = hasUsername || hasPassword;
        ValidateSource(hasAnyManualValue, credentialId, "SSH credentials");

        if (!credentialId.HasValue)
        {
            if (!hasUsername || !hasPassword)
            {
                throw new NetworkOperationCredentialException("Username and password are required for manual SSH authentication.");
            }

            return new SshCredential(username!.Trim(), password!);
        }

        var credential = await ResolveSavedCredentialAsync(credentialId.Value, cancellationToken);
        if (credential.Type != NetworkCredentialType.SshPassword)
        {
            throw new NetworkOperationCredentialException("The selected credential is not an SSH password credential.");
        }

        if (string.IsNullOrWhiteSpace(credential.Username) || string.IsNullOrEmpty(credential.Secret))
        {
            throw new NetworkOperationCredentialException("The selected credential cannot be used.");
        }

        return new SshCredential(credential.Username, credential.Secret);
    }

    private async Task<(NetworkCredentialType Type, string? Username, string Secret)> ResolveSavedCredentialAsync(
        int credentialId,
        CancellationToken cancellationToken)
    {
        if (credentialId <= 0)
        {
            throw new NetworkOperationCredentialException("Select a valid saved credential.");
        }

        try
        {
            return await credentialService.ResolveAsync(credentialId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                "Saved network credential {CredentialId} could not be resolved ({ErrorType}).",
                credentialId,
                exception.GetType().Name);
            throw new NetworkOperationCredentialException("The selected credential could not be found or decrypted.");
        }
    }

    private static void ValidateSource(bool hasManualValue, int? credentialId, string description)
    {
        if (hasManualValue && credentialId.HasValue)
        {
            throw new NetworkOperationCredentialException($"Choose either manual {description} or a saved credential, not both.");
        }

        if (!hasManualValue && !credentialId.HasValue)
        {
            throw new NetworkOperationCredentialException($"Provide manual {description} or select a saved credential.");
        }
    }
}
