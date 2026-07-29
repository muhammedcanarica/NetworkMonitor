using System.Text.Json;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Models;
using NetworkMonitor.Api.Services;

namespace NetworkMonitor.Api.Tests.Services;

public sealed class SnmpServiceTests
{
    private const string Community = "private-test-community";

    [Fact]
    public async Task GetAsync_WithValidRequestReturnsMappedValue()
    {
        var transport = new FakeSnmpTransport
        {
            GetHandler = (_, oids, _) => Task.FromResult<IReadOnlyList<SnmpVariableValue>>(
                [new SnmpVariableValue(oids[0], "core-switch", "OctetString")])
        };
        var service = new SnmpService(transport);

        var result = await service.GetAsync(
            "192.0.2.10",
            Community,
            ".1.3.6.1.2.1.1.5.0",
            2000,
            CancellationToken.None);

        Assert.Equal("1.3.6.1.2.1.1.5.0", result.Oid);
        Assert.Equal("core-switch", result.Value);
        Assert.Equal("OctetString", result.Type);
    }

    [Fact]
    public async Task GetAsync_WithInvalidOidRejectsRequestBeforeTransport()
    {
        var transport = new FakeSnmpTransport();
        var service = new SnmpService(transport);

        await Assert.ThrowsAsync<SnmpValidationException>(() => service.GetAsync(
            "192.0.2.10",
            Community,
            "1.3.invalid.1",
            2000,
            CancellationToken.None));

        Assert.Empty(transport.Connections);
    }

    [Fact]
    public async Task GetAsync_WithInvalidIpRejectsRequestBeforeTransport()
    {
        var transport = new FakeSnmpTransport();
        var service = new SnmpService(transport);

        await Assert.ThrowsAsync<SnmpValidationException>(() => service.GetAsync(
            "not-an-ip",
            Community,
            SnmpOids.System.Name,
            2000,
            CancellationToken.None));

        Assert.Empty(transport.Connections);
    }

    [Fact]
    public async Task GetAsync_PropagatesControlledTimeoutError()
    {
        var transport = new FakeSnmpTransport
        {
            GetHandler = (_, _, _) => throw new SnmpOperationException(
                SnmpErrorKind.Timeout,
                "SNMP request timed out.")
        };
        var service = new SnmpService(transport);

        var exception = await Assert.ThrowsAsync<SnmpOperationException>(() => service.GetAsync(
            "192.0.2.10",
            Community,
            SnmpOids.System.Name,
            500,
            CancellationToken.None));

        Assert.Equal(SnmpErrorKind.Timeout, exception.Kind);
        Assert.DoesNotContain(Community, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAsync_PropagatesCancellation()
    {
        var transport = new FakeSnmpTransport
        {
            GetHandler = async (_, _, token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return [];
            }
        };
        var service = new SnmpService(transport);
        using var cancellationSource = new CancellationTokenSource();

        var query = service.GetAsync(
            "192.0.2.10",
            Community,
            SnmpOids.System.Name,
            2000,
            cancellationSource.Token);
        await cancellationSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => query);
    }

    [Fact]
    public async Task WalkAsync_DefensivelyLimitsResultsTo500()
    {
        var transport = new FakeSnmpTransport
        {
            WalkHandler = (_, rootOid, maxResults, _) =>
            {
                Assert.Equal(SnmpService.MaxWalkResults, maxResults);
                return Task.FromResult<IReadOnlyList<SnmpVariableValue>>(
                    Enumerable.Range(1, 600)
                        .Select(index => new SnmpVariableValue(
                            $"{rootOid}.{index}",
                            index.ToString(),
                            "Integer32",
                            (ulong)index))
                        .ToList());
            }
        };
        var service = new SnmpService(transport);

        var result = await service.WalkAsync(
            "192.0.2.10",
            Community,
            "1.3.6.1.2.1",
            2000,
            CancellationToken.None);

        Assert.Equal(500, result.Count);
        Assert.Equal(500, result.Results.Count);
    }

    [Fact]
    public async Task GetSystemInfoAsync_MapsStandardSystemOidsAndAllowsMissingValues()
    {
        var transport = new FakeSnmpTransport
        {
            GetHandler = (_, _, _) => Task.FromResult<IReadOnlyList<SnmpVariableValue>>(
            [
                new(SnmpOids.System.Name, "core-switch-01", "OctetString"),
                new(SnmpOids.System.Description, "Test switch", "OctetString"),
                new(SnmpOids.System.ObjectId, "1.3.6.1.4.1.9", "ObjectIdentifier"),
                new(SnmpOids.System.UpTime, "1 day", "TimeTicks", 123456),
                new(SnmpOids.System.Location, "Server Room", "OctetString")
            ])
        };
        var service = new SnmpService(transport);

        var result = await service.GetSystemInfoAsync(
            "192.0.2.10",
            Community,
            2000,
            CancellationToken.None);

        Assert.Equal("core-switch-01", result.SysName);
        Assert.Equal("Test switch", result.SysDescription);
        Assert.Equal("1.3.6.1.4.1.9", result.SysObjectId);
        Assert.Equal((ulong)123456, result.SysUpTimeTicks);
        Assert.Equal("Server Room", result.SysLocation);
        Assert.Null(result.SysContact);
    }

    [Fact]
    public async Task GetInterfacesAsync_JoinsIfMibColumnsAndToleratesMissingValues()
    {
        var transport = new FakeSnmpTransport
        {
            WalkHandler = (_, rootOid, _, _) => Task.FromResult<IReadOnlyList<SnmpVariableValue>>(
                rootOid switch
                {
                    SnmpOids.Interfaces.Index =>
                    [
                        Value(rootOid, 1, "1", 1),
                        Value(rootOid, 2, "2", 2)
                    ],
                    SnmpOids.Interfaces.Description => [Value(rootOid, 1, "GigabitEthernet0/1")],
                    SnmpOids.Interfaces.Speed => [Value(rootOid, 1, "1000000000", 1000000000)],
                    SnmpOids.Interfaces.AdminStatus =>
                    [
                        Value(rootOid, 1, "1", 1),
                        Value(rootOid, 2, "3", 3)
                    ],
                    SnmpOids.Interfaces.OperStatus => [Value(rootOid, 1, "2", 2)],
                    _ => []
                })
        };
        var service = new SnmpService(transport);

        var result = await service.GetInterfacesAsync(
            "192.0.2.10",
            Community,
            2000,
            CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("GigabitEthernet0/1", result[0].Description);
        Assert.Equal("Up", result[0].AdminStatus);
        Assert.Equal("Down", result[0].OperStatus);
        Assert.Equal((ulong)1000000000, result[0].SpeedBitsPerSecond);
        Assert.Null(result[1].Description);
        Assert.Equal("Testing", result[1].AdminStatus);
        Assert.Equal("Unknown", result[1].OperStatus);
    }

    [Fact]
    public async Task ResponsesAndDiagnosticStrings_DoNotExposeCommunity()
    {
        var transport = new FakeSnmpTransport
        {
            GetHandler = (_, oids, _) => Task.FromResult<IReadOnlyList<SnmpVariableValue>>(
                [new SnmpVariableValue(oids[0], "value", "OctetString")])
        };
        var service = new SnmpService(transport);
        var request = new SnmpGetRequest
        {
            IpAddress = "192.0.2.10",
            Community = Community,
            Oid = SnmpOids.System.Name,
            TimeoutMilliseconds = 2000
        };

        var response = await service.GetAsync(
            request.IpAddress,
            request.Community,
            request.Oid,
            request.TimeoutMilliseconds,
            CancellationToken.None);

        var connection = Assert.Single(transport.Connections);
        Assert.DoesNotContain(Community, JsonSerializer.Serialize(response), StringComparison.Ordinal);
        Assert.DoesNotContain(Community, JsonSerializer.Serialize(connection), StringComparison.Ordinal);
        Assert.DoesNotContain(Community, request.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Community, connection.ToString(), StringComparison.Ordinal);
    }

    private static SnmpVariableValue Value(
        string rootOid,
        int index,
        string value,
        ulong? numericValue = null)
    {
        return new SnmpVariableValue($"{rootOid}.{index}", value, "Integer32", numericValue);
    }

    private sealed class FakeSnmpTransport : ISnmpTransport
    {
        public Func<SnmpConnection, IReadOnlyList<string>, CancellationToken, Task<IReadOnlyList<SnmpVariableValue>>> GetHandler { get; init; }
            = (_, _, _) => Task.FromResult<IReadOnlyList<SnmpVariableValue>>([]);

        public Func<SnmpConnection, string, int, CancellationToken, Task<IReadOnlyList<SnmpVariableValue>>> WalkHandler { get; init; }
            = (_, _, _, _) => Task.FromResult<IReadOnlyList<SnmpVariableValue>>([]);

        public List<SnmpConnection> Connections { get; } = [];

        public Task<IReadOnlyList<SnmpVariableValue>> GetAsync(
            SnmpConnection connection,
            IReadOnlyList<string> oids,
            CancellationToken cancellationToken)
        {
            Connections.Add(connection);
            return GetHandler(connection, oids, cancellationToken);
        }

        public Task<IReadOnlyList<SnmpVariableValue>> WalkAsync(
            SnmpConnection connection,
            string rootOid,
            int maxResults,
            CancellationToken cancellationToken)
        {
            Connections.Add(connection);
            return WalkHandler(connection, rootOid, maxResults, cancellationToken);
        }
    }
}
