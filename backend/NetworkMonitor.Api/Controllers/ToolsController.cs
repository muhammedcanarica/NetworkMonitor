using Microsoft.AspNetCore.Mvc;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Services;

namespace NetworkMonitor.Api.Controllers;

[ApiController]
[Route("api/tools")]
public sealed class ToolsController(
    IIpScannerService ipScannerService,
    IWakeOnLanService wakeOnLanService,
    IPortScannerService portScannerService) : ControllerBase
{
    [HttpPost("ip-scan")]
    [ProducesResponseType<IpScanResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IpScanResponse>> ScanIpAddresses(
        IpScanRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await ipScannerService.ScanAsync(request.Cidr, cancellationToken));
        }
        catch (IpScanValidationException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid IP scan request",
                Detail = exception.Message
            });
        }
    }

    [HttpPost("wake-on-lan")]
    [ProducesResponseType<WakeOnLanResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<WakeOnLanResponse>> SendWakeOnLan(
        WakeOnLanRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await wakeOnLanService.SendAsync(request, cancellationToken));
        }
        catch (WakeOnLanValidationException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Wake-on-LAN request",
                Detail = exception.Message
            });
        }
        catch (WakeOnLanOperationException exception)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new ProblemDetails
            {
                Status = StatusCodes.Status502BadGateway,
                Title = "Wake-on-LAN request failed",
                Detail = exception.Message
            });
        }
    }

    [HttpPost("port-scanner")]
    [ProducesResponseType<PortScanResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<PortScanResponse>> ScanPorts(
        PortScanRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await portScannerService.ScanAsync(request, cancellationToken));
        }
        catch (PortScanValidationException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid port scan request",
                Detail = exception.Message
            });
        }
        catch (PortScanOperationException exception)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new ProblemDetails
            {
                Status = StatusCodes.Status502BadGateway,
                Title = "TCP port scan failed",
                Detail = exception.Message
            });
        }
    }
}
