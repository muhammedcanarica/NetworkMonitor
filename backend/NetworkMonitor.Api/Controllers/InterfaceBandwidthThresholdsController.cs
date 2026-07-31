using Microsoft.AspNetCore.Mvc;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Services;

namespace NetworkMonitor.Api.Controllers;

[ApiController]
[Route("api/devices/{deviceId:int}/interfaces/{interfaceIndex:int}/bandwidth-threshold")]
public sealed class InterfaceBandwidthThresholdsController(IInterfaceBandwidthThresholdService service) : ControllerBase
{
    [HttpGet]
    public Task<ActionResult<InterfaceBandwidthThresholdResponse?>> Get(int deviceId, int interfaceIndex, CancellationToken token)
        => Execute(() => service.GetAsync(deviceId, interfaceIndex, token));

    [HttpPut]
    public Task<ActionResult<InterfaceBandwidthThresholdResponse>> Update(int deviceId, int interfaceIndex, UpdateInterfaceBandwidthThresholdRequest request, CancellationToken token)
        => Execute(() => service.UpdateAsync(deviceId, interfaceIndex, request, token));

    [HttpDelete]
    public async Task<IActionResult> Delete(int deviceId, int interfaceIndex, CancellationToken token)
    {
        try { await service.DeleteAsync(deviceId, interfaceIndex, token); return NoContent(); }
        catch (InterfaceBandwidthThresholdNotFoundException exception) { return NotFound(Problem(404, "Bandwidth threshold was not found", exception.Message)); }
        catch (InterfaceBandwidthThresholdConflictException exception) { return Conflict(Problem(409, "Bandwidth threshold is in use", exception.Message)); }
    }

    private async Task<ActionResult<T>> Execute<T>(Func<Task<T>> operation)
    {
        try { return Ok(await operation()); }
        catch (InterfaceBandwidthThresholdNotFoundException exception) { return NotFound(Problem(404, "Bandwidth threshold resource was not found", exception.Message)); }
        catch (InterfaceBandwidthThresholdValidationException exception) { return BadRequest(Problem(400, "Invalid bandwidth threshold", exception.Message)); }
    }

    private static ProblemDetails Problem(int status, string title, string detail) => new() { Status = status, Title = title, Detail = detail };
}
