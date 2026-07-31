using Microsoft.AspNetCore.Mvc;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Services;

namespace NetworkMonitor.Api.Controllers;

[ApiController]
[Route("api/topology")]
public sealed class TopologyController(ITopologyDiscoveryService topologyDiscoveryService) : ControllerBase
{
    [HttpPost("discover")]
    [ProducesResponseType<TopologyDiscoveryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TopologyDiscoveryResponse>> Discover(
        TopologyDiscoveryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await topologyDiscoveryService.DiscoverAsync(request, cancellationToken));
        }
        catch (TopologyDiscoveryValidationException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid topology discovery request",
                Detail = exception.Message
            });
        }
    }
}
