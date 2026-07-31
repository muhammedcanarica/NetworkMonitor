using Microsoft.AspNetCore.Mvc;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Services;

namespace NetworkMonitor.Api.Controllers;

[ApiController]
[Route("api/topology")]
public sealed class TopologyController(
    ITopologyDiscoveryService topologyDiscoveryService,
    INetworkOperationCredentialResolver credentialResolver) : ControllerBase
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
            var community = await credentialResolver.ResolveSnmpCommunityAsync(
                request.Community,
                request.CredentialId,
                cancellationToken);
            var resolvedRequest = new TopologyDiscoveryRequest
            {
                DeviceIds = request.DeviceIds,
                Community = community,
                TimeoutMilliseconds = request.TimeoutMilliseconds
            };
            return Ok(await topologyDiscoveryService.DiscoverAsync(resolvedRequest, cancellationToken));
        }
        catch (NetworkOperationCredentialException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid SNMP credential",
                Detail = exception.Message
            });
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
