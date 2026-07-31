using Microsoft.AspNetCore.Mvc;
using NetworkMonitor.Api.Controllers;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Services;

namespace NetworkMonitor.Api.Tests.Controllers;

public sealed class TopologyControllerTests
{
    [Fact]
    public async Task Discover_ReturnsTopologyResponse()
    {
        var expected = new TopologyDiscoveryResponse([], [], 1, 1, 0, 12, []);
        var controller = new TopologyController(new StubTopologyService((_, _) => Task.FromResult(expected)));

        var action = await controller.Discover(new TopologyDiscoveryRequest { DeviceIds = [1], Community = "private" }, CancellationToken.None);

        var result = Assert.IsType<OkObjectResult>(action.Result);
        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task Discover_MapsValidationToBadRequestWithoutCommunity()
    {
        var controller = new TopologyController(new StubTopologyService((_, _) =>
            throw new TopologyDiscoveryValidationException("Invalid selection.")));

        var action = await controller.Discover(new TopologyDiscoveryRequest { DeviceIds = [1], Community = "sensitive" }, CancellationToken.None);

        var result = Assert.IsType<BadRequestObjectResult>(action.Result);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal("Invalid selection.", problem.Detail);
        Assert.DoesNotContain("sensitive", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StubTopologyService(
        Func<TopologyDiscoveryRequest, CancellationToken, Task<TopologyDiscoveryResponse>> discover) : ITopologyDiscoveryService
    {
        public Task<TopologyDiscoveryResponse> DiscoverAsync(TopologyDiscoveryRequest request, CancellationToken cancellationToken) => discover(request, cancellationToken);
    }
}
