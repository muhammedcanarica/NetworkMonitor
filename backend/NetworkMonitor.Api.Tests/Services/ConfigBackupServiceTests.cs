using Microsoft.Extensions.Options;
using NetworkMonitor.Api.Configuration;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Services;

namespace NetworkMonitor.Api.Tests.Services;

public sealed class ConfigBackupServiceTests
{
    [Fact]
    public async Task GetRunningConfigurationAsync_ReturnsMappedCiscoConfiguration()
    {
        var transport = new RecordingSshCommandTransport("hostname core-switch\nend\n");
        var service = CreateService(transport);
        var request = CreateRequest();

        var response = await service.GetRunningConfigurationAsync(request, CancellationToken.None);

        Assert.Equal("192.168.1.10", response.IpAddress);
        Assert.Equal(ConfigBackupVendor.CiscoIos, response.Vendor);
        Assert.Equal("hostname core-switch\nend\n", response.Configuration);
        Assert.DoesNotContain("do-not-log-me", response.Configuration, StringComparison.Ordinal);
        Assert.Matches("^192\\.168\\.1\\.10-running-config-\\d{4}-\\d{2}-\\d{2}\\.txt$", response.SuggestedFileName);
        Assert.Equal(["show running-config"], transport.Commands);
        Assert.Equal("192.168.1.10", transport.Connection!.IpAddress);
        Assert.Equal(22, transport.Connection.Port);
        Assert.Equal("operator", transport.Connection.Username);
        Assert.Equal("do-not-log-me", transport.Connection.Password);
        Assert.Equal(10000, transport.Timeouts!.ConnectionTimeoutMilliseconds);
        Assert.Equal(30000, transport.Timeouts.CommandTimeoutMilliseconds);
    }

    [Theory]
    [InlineData("not-an-ip", 22, "operator", "secret", ConfigBackupVendor.CiscoIos, "IPv4")]
    [InlineData("192.168.1.10", 0, "operator", "secret", ConfigBackupVendor.CiscoIos, "between 1 and 65535")]
    [InlineData("192.168.1.10", 22, "", "secret", ConfigBackupVendor.CiscoIos, "Username")]
    [InlineData("192.168.1.10", 22, "operator", "", ConfigBackupVendor.CiscoIos, "Password")]
    [InlineData("192.168.1.10", 22, "operator", "secret", (ConfigBackupVendor)99, "Cisco")]
    public async Task GetRunningConfigurationAsync_RejectsInvalidRequests(
        string ipAddress,
        int port,
        string username,
        string password,
        ConfigBackupVendor vendor,
        string expectedMessage)
    {
        var transport = new RecordingSshCommandTransport("ignored");
        var service = CreateService(transport);

        var exception = await Assert.ThrowsAsync<ConfigBackupValidationException>(() =>
            service.GetRunningConfigurationAsync(
                new ConfigBackupRequest
                {
                    IpAddress = ipAddress,
                    Port = port,
                    Username = username,
                    Password = password,
                    Vendor = vendor
                },
                CancellationToken.None));

        Assert.Contains(expectedMessage, exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(transport.Connection);
    }

    [Theory]
    [InlineData(ConfigBackupErrorKind.Authentication, "authentication")]
    [InlineData(ConfigBackupErrorKind.ConnectionTimeout, "connection")]
    [InlineData(ConfigBackupErrorKind.CommandTimeout, "running configuration")]
    [InlineData(ConfigBackupErrorKind.Connection, "connection")]
    public async Task GetRunningConfigurationAsync_MapsTransportFailuresWithoutLeakingPassword(
        ConfigBackupErrorKind kind,
        string expectedMessage)
    {
        const string password = "do-not-log-me";
        var service = CreateService(new ThrowingSshCommandTransport(kind));

        var exception = await Assert.ThrowsAsync<ConfigBackupOperationException>(() =>
            service.GetRunningConfigurationAsync(CreateRequest(password), CancellationToken.None));

        Assert.Equal(kind, exception.Kind);
        Assert.Contains(expectedMessage, exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(password, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetRunningConfigurationAsync_PropagatesCancellation()
    {
        var service = CreateService(new DelayingSshCommandTransport());
        using var cancellationSource = new CancellationTokenSource();

        var operation = service.GetRunningConfigurationAsync(CreateRequest(), cancellationSource.Token);
        await cancellationSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
    }

    [Fact]
    public void SensitiveRequestAndConnectionDiagnosticsAreRedacted()
    {
        const string password = "do-not-log-me";
        var request = CreateRequest(password);
        var connection = new SshCommandConnection("192.168.1.10", 22, "operator", password);

        Assert.DoesNotContain(password, request.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(password, connection.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("operator", request.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("operator", connection.ToString(), StringComparison.Ordinal);
    }

    private static ConfigBackupService CreateService(ISshCommandTransport transport)
    {
        return new ConfigBackupService(
            transport,
            Options.Create(new ConfigBackupOptions
            {
                ConnectionTimeoutMilliseconds = 10000,
                CommandTimeoutMilliseconds = 30000
            }));
    }

    private static ConfigBackupRequest CreateRequest(string password = "do-not-log-me") => new()
    {
        IpAddress = "192.168.1.10",
        Port = 22,
        Username = "operator",
        Password = password,
        Vendor = ConfigBackupVendor.CiscoIos
    };

    private sealed class RecordingSshCommandTransport(string result) : ISshCommandTransport
    {
        public SshCommandConnection? Connection { get; private set; }

        public IReadOnlyList<string>? Commands { get; private set; }

        public SshCommandTimeouts? Timeouts { get; private set; }

        public Task<string> ExecuteAsync(
            SshCommandConnection connection,
            IReadOnlyList<string> commands,
            SshCommandTimeouts timeouts,
            CancellationToken cancellationToken)
        {
            Connection = connection;
            Commands = commands;
            Timeouts = timeouts;
            return Task.FromResult(result);
        }
    }

    private sealed class ThrowingSshCommandTransport(ConfigBackupErrorKind kind) : ISshCommandTransport
    {
        public Task<string> ExecuteAsync(
            SshCommandConnection connection,
            IReadOnlyList<string> commands,
            SshCommandTimeouts timeouts,
            CancellationToken cancellationToken)
        {
            throw new SshCommandTransportException(kind, "Raw transport error with do-not-log-me.");
        }
    }

    private sealed class DelayingSshCommandTransport : ISshCommandTransport
    {
        public async Task<string> ExecuteAsync(
            SshCommandConnection connection,
            IReadOnlyList<string> commands,
            SshCommandTimeouts timeouts,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return string.Empty;
        }
    }
}
