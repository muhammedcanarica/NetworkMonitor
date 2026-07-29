using Microsoft.AspNetCore.Mvc;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Services;

namespace NetworkMonitor.Api.Controllers;

[ApiController]
[Route("api/tools")]
public sealed class ToolsController(IIpScannerService ipScannerService) : ControllerBase
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
}
