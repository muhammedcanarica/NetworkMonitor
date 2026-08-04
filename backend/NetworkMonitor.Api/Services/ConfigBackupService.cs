using System.Net;
using Microsoft.Extensions.Options;
using NetworkMonitor.Api.Configuration;
using NetworkMonitor.Api.Dtos;

namespace NetworkMonitor.Api.Services;

public sealed class ConfigBackupService(
    ISshCommandTransport sshCommandTransport,
    ConfigBackupProviderResolver providerResolver,
    IOptions<ConfigBackupOptions> options) : IConfigBackupService
{
    private readonly ConfigBackupOptions _options = options.Value;

    public async Task<ConfigBackupResponse> GetRunningConfigurationAsync(
        ConfigBackupRequest request,
        CancellationToken cancellationToken)
    {
        var provider = providerResolver.Resolve(request.Vendor);
        var connection = CreateConnection(request);
        var timeouts = new SshCommandTimeouts(
            _options.ConnectionTimeoutMilliseconds,
            _options.CommandTimeoutMilliseconds);
        string configuration;

        try
        {
            configuration = await sshCommandTransport.ExecuteAsync(
                connection,
                provider.GetRunningConfigurationCommands(),
                timeouts,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SshCommandTransportException exception)
        {
            throw new ConfigBackupOperationException(
                exception.Kind,
                GetSafeOperationMessage(exception.Kind),
                exception);
        }
        catch (Exception exception)
        {
            throw new ConfigBackupOperationException(
                ConfigBackupErrorKind.Unexpected,
                GetSafeOperationMessage(ConfigBackupErrorKind.Unexpected),
                exception);
        }

        var capturedAt = DateTimeOffset.UtcNow;
        return new ConfigBackupResponse(
            connection.IpAddress,
            request.Vendor,
            configuration,
            capturedAt,
            CreateSuggestedFileName(connection.IpAddress, capturedAt));
    }

    private static SshCommandConnection CreateConnection(ConfigBackupRequest request)
    {
        if (!IPAddress.TryParse(request.IpAddress?.Trim(), out var address)
            || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            throw new ConfigBackupValidationException("Target IP address must be a valid IPv4 address.");
        }

        if (request.Port is < 1 or > 65535)
        {
            throw new ConfigBackupValidationException("SSH port must be between 1 and 65535.");
        }

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            throw new ConfigBackupValidationException("Username is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ConfigBackupValidationException("Password is required.");
        }

        return new SshCommandConnection(
            address.ToString(),
            request.Port,
            request.Username,
            request.Password);
    }

    private static string GetSafeOperationMessage(ConfigBackupErrorKind kind)
    {
        return kind switch
        {
            ConfigBackupErrorKind.Authentication => "SSH authentication failed. Check the supplied username and password.",
            ConfigBackupErrorKind.Connection => "The SSH connection to the target device could not be established.",
            ConfigBackupErrorKind.ConnectionTimeout => "The SSH connection to the target device timed out.",
            ConfigBackupErrorKind.CommandTimeout => "The running configuration command timed out.",
            _ => "The configuration backup could not be completed due to an unexpected SSH or network error."
        };
    }

    private static string CreateSuggestedFileName(string ipAddress, DateTimeOffset capturedAt)
    {
        var validName = string.Concat(ipAddress.Where(character => !Path.GetInvalidFileNameChars().Contains(character)));
        return $"{validName}-running-config-{capturedAt:yyyy-MM-dd}.txt";
    }
}
