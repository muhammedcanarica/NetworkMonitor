using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
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
        var controller = CreateController(new StubScannerService((_, _) =>
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
        var controller = CreateController(new StubScannerService((_, _) =>
            throw new IpScanValidationException("Invalid CIDR.")));

        var action = await controller.ScanIpAddresses(
            new IpScanRequest("invalid"),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(action.Result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal("Invalid CIDR.", problem.Detail);
    }

    [Fact]
    public async Task SendWakeOnLan_ReturnsMagicPacketSentResponse()
    {
        var response = new WakeOnLanResponse(
            "00:11:22:33:44:55",
            "255.255.255.255",
            9,
            "Magic packet sent.");
        var controller = CreateController(
            new StubScannerService((_, _) => throw new InvalidOperationException()),
            new StubWakeOnLanService((_, _) => Task.FromResult(response)));

        var action = await controller.SendWakeOnLan(
            new WakeOnLanRequest("00:11:22:33:44:55", "255.255.255.255"),
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        Assert.Same(response, ok.Value);
    }

    [Fact]
    public async Task SendWakeOnLan_MapsValidationFailureToBadRequest()
    {
        var controller = CreateController(
            new StubScannerService((_, _) => throw new InvalidOperationException()),
            new StubWakeOnLanService((_, _) =>
                throw new WakeOnLanValidationException("Invalid MAC address.")));

        var action = await controller.SendWakeOnLan(
            new WakeOnLanRequest("invalid", "255.255.255.255"),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(action.Result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal("Invalid MAC address.", problem.Detail);
    }

    [Fact]
    public async Task SendWakeOnLan_MapsNetworkFailureToBadGateway()
    {
        var controller = CreateController(
            new StubScannerService((_, _) => throw new InvalidOperationException()),
            new StubWakeOnLanService((_, _) => throw new WakeOnLanOperationException(
                "The magic packet could not be sent.",
                new IOException())));

        var action = await controller.SendWakeOnLan(
            new WakeOnLanRequest("00:11:22:33:44:55", "255.255.255.255"),
            CancellationToken.None);

        var result = Assert.IsType<ObjectResult>(action.Result);
        Assert.Equal(StatusCodes.Status502BadGateway, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal("The magic packet could not be sent.", problem.Detail);
    }

    [Fact]
    public async Task ScanPorts_ReturnsPortScanResponse()
    {
        var response = new PortScanResponse(
            "127.0.0.1",
            2,
            1,
            4,
            [
                new PortScanResult(22, PortState.Open, 1, "SSH"),
                new PortScanResult(80, PortState.Closed, null, "HTTP")
            ]);
        var controller = CreateController(
            new StubScannerService((_, _) => throw new InvalidOperationException()),
            portScannerService: new StubPortScannerService((_, _) => Task.FromResult(response)));

        var action = await controller.ScanPorts(
            new PortScanRequest { IpAddress = "127.0.0.1", Ports = [22, 80], TimeoutMilliseconds = 1000 },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        Assert.Same(response, ok.Value);
    }

    [Fact]
    public async Task ScanPorts_MapsValidationFailureToBadRequest()
    {
        var controller = CreateController(
            new StubScannerService((_, _) => throw new InvalidOperationException()),
            portScannerService: new StubPortScannerService((_, _) =>
                throw new PortScanValidationException("Invalid port.")));

        var action = await controller.ScanPorts(
            new PortScanRequest { IpAddress = "127.0.0.1", Ports = [0], TimeoutMilliseconds = 1000 },
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(action.Result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal("Invalid port.", problem.Detail);
    }

    [Fact]
    public async Task ScanPorts_MapsNetworkFailureToBadGateway()
    {
        var controller = CreateController(
            new StubScannerService((_, _) => throw new InvalidOperationException()),
            portScannerService: new StubPortScannerService((_, _) => throw new PortScanOperationException(
                "The TCP port scan could not be completed.",
                new IOException())));

        var action = await controller.ScanPorts(
            new PortScanRequest { IpAddress = "127.0.0.1", Ports = [80], TimeoutMilliseconds = 1000 },
            CancellationToken.None);

        var result = Assert.IsType<ObjectResult>(action.Result);
        Assert.Equal(StatusCodes.Status502BadGateway, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal("The TCP port scan could not be completed.", problem.Detail);
    }

    private static ToolsController CreateController(
        IIpScannerService scannerService,
        IWakeOnLanService? wakeOnLanService = null,
        IPortScannerService? portScannerService = null)
    {
        return new ToolsController(
            scannerService,
            wakeOnLanService ?? new StubWakeOnLanService((_, _) => throw new InvalidOperationException()),
            portScannerService ?? new StubPortScannerService((_, _) => throw new InvalidOperationException()));
    }

    private sealed class StubScannerService(
        Func<string, CancellationToken, Task<IpScanResponse>> scan) : IIpScannerService
    {
        public Task<IpScanResponse> ScanAsync(string cidr, CancellationToken cancellationToken)
        {
            return scan(cidr, cancellationToken);
        }
    }

    private sealed class StubWakeOnLanService(
        Func<WakeOnLanRequest, CancellationToken, Task<WakeOnLanResponse>> send) : IWakeOnLanService
    {
        public Task<WakeOnLanResponse> SendAsync(
            WakeOnLanRequest request,
            CancellationToken cancellationToken)
        {
            return send(request, cancellationToken);
        }
    }

    private sealed class StubPortScannerService(
        Func<PortScanRequest, CancellationToken, Task<PortScanResponse>> scan) : IPortScannerService
    {
        public Task<PortScanResponse> ScanAsync(
            PortScanRequest request,
            CancellationToken cancellationToken)
        {
            return scan(request, cancellationToken);
        }
    }
}
