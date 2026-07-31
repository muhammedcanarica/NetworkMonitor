using Microsoft.AspNetCore.Mvc;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Services;

namespace NetworkMonitor.Api.Controllers;

[ApiController]
[Route("api/devices/{deviceId:int}")]
public sealed class SnmpMonitoringController(ISnmpMonitoringConfigurationService service) : ControllerBase
{
    [HttpGet("snmp-monitoring")]
    public Task<ActionResult<SnmpMonitoringProfileResponse?>> Get(int deviceId, CancellationToken token)
        => Execute(() => service.GetAsync(deviceId, token));

    [HttpPost("snmp-monitoring/interfaces")]
    public Task<ActionResult<IReadOnlyList<SnmpInterfaceResponse>>> DiscoverInterfaces(int deviceId, DiscoverMonitoringInterfacesRequest request, CancellationToken token)
        => Execute(() => service.DiscoverInterfacesAsync(deviceId, request, token));

    [HttpPut("snmp-monitoring")]
    public Task<ActionResult<SnmpMonitoringProfileResponse>> Update(int deviceId, UpdateSnmpMonitoringRequest request, CancellationToken token)
        => Execute(() => service.UpdateAsync(deviceId, request, token));

    [HttpDelete("snmp-monitoring")]
    public Task<ActionResult<object?>> Disable(int deviceId, CancellationToken token)
        => Execute<object?>(async () => { await service.DisableAsync(deviceId, token); return null; }, noContent: true);

    [HttpGet("interface-traffic")]
    public Task<ActionResult<IReadOnlyList<InterfaceTrafficSummaryResponse>>> Summary(int deviceId, CancellationToken token)
        => Execute(() => service.GetSummaryAsync(deviceId, token));

    [HttpGet("interfaces/{interfaceIndex:int}/traffic")]
    public Task<ActionResult<InterfaceTrafficHistoryResponse>> History(int deviceId, int interfaceIndex, [FromQuery] int hours = 24, CancellationToken token = default)
        => Execute(() => service.GetHistoryAsync(deviceId, interfaceIndex, hours, token));

    private async Task<ActionResult<T>> Execute<T>(Func<Task<T>> operation, bool noContent = false)
    {
        try
        {
            var result = await operation();
            return noContent ? NoContent() : Ok(result);
        }
        catch (SnmpMonitoringNotFoundException exception)
        {
            return NotFound(Problem(404, "SNMP monitoring resource was not found", exception.Message));
        }
        catch (SnmpMonitoringValidationException exception)
        {
            return BadRequest(Problem(400, "Invalid SNMP monitoring request", exception.Message));
        }
        catch (NetworkOperationCredentialException exception)
        {
            return BadRequest(Problem(400, "Saved SNMP credential could not be used", exception.Message));
        }
        catch (SnmpOperationException exception)
        {
            var status = exception.Kind == SnmpErrorKind.Timeout ? 504 : 502;
            return StatusCode(status, Problem(status, "SNMP interface discovery failed", exception.Message));
        }
    }

    private static ProblemDetails Problem(int status, string title, string detail) => new() { Status = status, Title = title, Detail = detail };
}
