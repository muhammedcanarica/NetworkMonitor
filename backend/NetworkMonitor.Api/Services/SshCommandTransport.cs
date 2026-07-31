using System.Net.Sockets;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace NetworkMonitor.Api.Services;

public sealed class SshCommandTransport(ILogger<SshCommandTransport> logger) : ISshCommandTransport
{
    public async Task<string> ExecuteAsync(
        SshCommandConnection connection,
        IReadOnlyList<string> commands,
        SshCommandTimeouts timeouts,
        CancellationToken cancellationToken)
    {
        var connectionInfo = new Renci.SshNet.ConnectionInfo(
            connection.IpAddress,
            connection.Port,
            connection.Username,
            new PasswordAuthenticationMethod(connection.Username, connection.Password))
        {
            Timeout = TimeSpan.FromMilliseconds(timeouts.ConnectionTimeoutMilliseconds)
        };
        using var client = new SshClient(connectionInfo);

        try
        {
            await ConnectAsync(client, connection, timeouts.ConnectionTimeoutMilliseconds, cancellationToken);
            return await ExecuteCommandsAsync(client, connection, commands, timeouts.CommandTimeoutMilliseconds, cancellationToken);
        }
        finally
        {
            if (client.IsConnected)
            {
                try
                {
                    client.Disconnect();
                }
                catch (Exception exception)
                {
                    logger.LogDebug(
                        "SSH configuration backup connection to {IpAddress} could not be closed cleanly ({ErrorType}).",
                        connection.IpAddress,
                        exception.GetType().Name);
                }
            }
        }
    }

    private async Task ConnectAsync(
        SshClient client,
        SshCommandConnection connection,
        int timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromMilliseconds(timeoutMilliseconds));

        try
        {
            await client.ConnectAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            throw new SshCommandTransportException(
                ConfigBackupErrorKind.ConnectionTimeout,
                "SSH connection timed out.",
                exception);
        }
        catch (SshOperationTimeoutException exception)
        {
            throw new SshCommandTransportException(
                ConfigBackupErrorKind.ConnectionTimeout,
                "SSH connection timed out.",
                exception);
        }
        catch (SshAuthenticationException exception)
        {
            LogFailure(connection.IpAddress, ConfigBackupErrorKind.Authentication, exception);
            throw new SshCommandTransportException(
                ConfigBackupErrorKind.Authentication,
                "SSH authentication failed.",
                exception);
        }
        catch (SocketException exception)
        {
            LogFailure(connection.IpAddress, ConfigBackupErrorKind.Connection, exception);
            throw new SshCommandTransportException(
                ConfigBackupErrorKind.Connection,
                "SSH connection failed.",
                exception);
        }
        catch (SshConnectionException exception)
        {
            LogFailure(connection.IpAddress, ConfigBackupErrorKind.Connection, exception);
            throw new SshCommandTransportException(
                ConfigBackupErrorKind.Connection,
                "SSH connection failed.",
                exception);
        }
        catch (Exception exception)
        {
            LogFailure(connection.IpAddress, ConfigBackupErrorKind.Unexpected, exception);
            throw new SshCommandTransportException(
                ConfigBackupErrorKind.Unexpected,
                "SSH connection failed unexpectedly.",
                exception);
        }
    }

    private async Task<string> ExecuteCommandsAsync(
        SshClient client,
        SshCommandConnection connection,
        IReadOnlyList<string> commands,
        int timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        var result = string.Empty;
        foreach (var commandText in commands)
        {
            using var command = client.CreateCommand(commandText);
            command.CommandTimeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);
            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(TimeSpan.FromMilliseconds(timeoutMilliseconds));

            try
            {
                await command.ExecuteAsync(timeoutSource.Token);
                result = command.Result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException exception)
            {
                throw new SshCommandTransportException(
                    ConfigBackupErrorKind.CommandTimeout,
                    "SSH command timed out.",
                    exception);
            }
            catch (SshOperationTimeoutException exception)
            {
                throw new SshCommandTransportException(
                    ConfigBackupErrorKind.CommandTimeout,
                    "SSH command timed out.",
                    exception);
            }
            catch (SocketException exception)
            {
                LogFailure(connection.IpAddress, ConfigBackupErrorKind.Connection, exception);
                throw new SshCommandTransportException(
                    ConfigBackupErrorKind.Connection,
                    "SSH connection failed while executing the command.",
                    exception);
            }
            catch (SshConnectionException exception)
            {
                LogFailure(connection.IpAddress, ConfigBackupErrorKind.Connection, exception);
                throw new SshCommandTransportException(
                    ConfigBackupErrorKind.Connection,
                    "SSH connection failed while executing the command.",
                    exception);
            }
            catch (Exception exception)
            {
                LogFailure(connection.IpAddress, ConfigBackupErrorKind.Unexpected, exception);
                throw new SshCommandTransportException(
                    ConfigBackupErrorKind.Unexpected,
                    "SSH command failed unexpectedly.",
                    exception);
            }
        }

        return result;
    }

    private void LogFailure(string ipAddress, ConfigBackupErrorKind kind, Exception exception)
    {
        logger.LogWarning(
            "SSH configuration backup to {IpAddress} failed with {ErrorKind} ({ErrorType}).",
            ipAddress,
            kind,
            exception.GetType().Name);
    }
}
