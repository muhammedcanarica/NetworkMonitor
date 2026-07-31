using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NetworkMonitor.Api.Controllers;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Tests.Infrastructure;
using NetworkMonitor.Api.Services;

namespace NetworkMonitor.Api.Tests.Controllers;

public sealed class SnmpControllerTests
{
    [Fact]
    public async Task Get_ResolvesSavedCredentialWithoutReturningCommunity()
    {
        const string secret = "stored-community";
        var service = new StubSnmpService
        {
            GetHandler = (_, community, oid, _, _) =>
            {
                Assert.Equal(secret, community);
                return Task.FromResult(new SnmpValueResponse(oid, "router-1", "OctetString"));
            }
        };
        var resolver = new StubNetworkOperationCredentialResolver
        {
            SnmpHandler = (community, credentialId, _) =>
            {
                Assert.Null(community);
                Assert.Equal(7, credentialId);
                return Task.FromResult(secret);
            }
        };
        var controller = new SnmpController(service, resolver, NullLogger<SnmpController>.Instance);

        var action = await controller.Get(new SnmpGetRequest
        {
            IpAddress = "192.0.2.10",
            CredentialId = 7,
            Oid = "1.3.6.1.2.1.1.5.0"
        }, CancellationToken.None);

        var result = Assert.IsType<OkObjectResult>(action.Result);
        Assert.DoesNotContain(secret, result.Value!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_MapsCredentialFailureToBadRequest()
    {
        var resolver = new StubNetworkOperationCredentialResolver
        {
            SnmpHandler = (_, _, _) => throw new NetworkOperationCredentialException("Saved credential could not be used.")
        };
        var controller = new SnmpController(new StubSnmpService(), resolver, NullLogger<SnmpController>.Instance);

        var action = await controller.Get(new SnmpGetRequest { IpAddress = "192.0.2.10", CredentialId = 99, Oid = "1.3" }, CancellationToken.None);

        var result = Assert.IsType<BadRequestObjectResult>(action.Result);
        Assert.Equal("Saved credential could not be used.", Assert.IsType<ProblemDetails>(result.Value).Detail);
    }

    [Fact]
    public async Task Get_MapsTimeoutToGatewayTimeoutWithoutRawDetails()
    {
        var service = new StubSnmpService
        {
            GetHandler = (_, _, _, _, _) => throw new SnmpOperationException(
                SnmpErrorKind.Timeout,
                "The SNMP request timed out.",
                new InvalidOperationException("raw transport details"))
        };
        var controller = new SnmpController(service, new StubNetworkOperationCredentialResolver(), NullLogger<SnmpController>.Instance);

        var action = await controller.Get(new SnmpGetRequest
        {
            IpAddress = "192.0.2.10",
            Community = "secret",
            Oid = "1.3.6.1.2.1.1.5.0",
            TimeoutMilliseconds = 500
        }, CancellationToken.None);

        var result = Assert.IsType<ObjectResult>(action.Result);
        Assert.Equal(504, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal("The SNMP request timed out.", problem.Detail);
        Assert.DoesNotContain("raw transport details", problem.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", problem.Detail, StringComparison.Ordinal);
    }

    private sealed class StubSnmpService : ISnmpService
    {
        public Func<string, string, string, int, CancellationToken, Task<SnmpValueResponse>> GetHandler { get; init; }
            = (_, _, _, _, _) => Task.FromResult(new SnmpValueResponse("1.3", null, "Null"));

        public Task<SnmpValueResponse> GetAsync(string ipAddress, string community, string oid, int timeoutMilliseconds, CancellationToken cancellationToken)
            => GetHandler(ipAddress, community, oid, timeoutMilliseconds, cancellationToken);

        public Task<SnmpWalkResponse> WalkAsync(string ipAddress, string community, string rootOid, int timeoutMilliseconds, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<SnmpSystemInfoResponse> GetSystemInfoAsync(string ipAddress, string community, int timeoutMilliseconds, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<SnmpInterfaceResponse>> GetInterfacesAsync(string ipAddress, string community, int timeoutMilliseconds, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
