using Microsoft.AspNetCore.Mvc;
using NetworkMonitor.Api.Controllers;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Services;

namespace NetworkMonitor.Api.Tests.Controllers;

public sealed class ToolsControllerTests
{
    [Fact]
    public async Task ScanIpAddresses_ReturnsScanResponse()
    {
        var response = new IpScanResponse("127.0.0.1/32", 1, 0, 2, []);
        var controller = new ToolsController(new StubScannerService((_, _) =>
            Task.FromResult(response)));

        var action = await controller.ScanIpAddresses(
            new IpScanRequest("127.0.0.1/32"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        Assert.Same(response, ok.Value);
    }

    [Fact]
    public async Task ScanIpAddresses_MapsValidationFailureToBadRequest()
    {
        var controller = new ToolsController(new StubScannerService((_, _) =>
            throw new IpScanValidationException("Invalid CIDR.")));

        var action = await controller.ScanIpAddresses(
            new IpScanRequest("invalid"),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(action.Result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal("Invalid CIDR.", problem.Detail);
    }

    private sealed class StubScannerService(
        Func<string, CancellationToken, Task<IpScanResponse>> scan) : IIpScannerService
    {
        public Task<IpScanResponse> ScanAsync(string cidr, CancellationToken cancellationToken)
        {
            return scan(cidr, cancellationToken);
        }
    }
}
