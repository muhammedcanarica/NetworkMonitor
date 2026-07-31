namespace NetworkMonitor.Api.Services;

public interface ISshCommandTransport
{
    Task<string> ExecuteAsync(
        SshCommandConnection connection,
        IReadOnlyList<string> commands,
        SshCommandTimeouts timeouts,
        CancellationToken cancellationToken);
}

public sealed record SshCommandConnection(
    string IpAddress,
    int Port,
    string Username,
    string Password)
{
    public override string ToString()
    {
        return $"SshCommandConnection {{ IpAddress = {IpAddress}, Port = {Port}, Username = [REDACTED], Password = [REDACTED] }}";
    }
}

public sealed record SshCommandTimeouts(
    int ConnectionTimeoutMilliseconds,
    int CommandTimeoutMilliseconds);
