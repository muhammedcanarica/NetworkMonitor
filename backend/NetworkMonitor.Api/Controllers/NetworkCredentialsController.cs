using Microsoft.AspNetCore.Mvc;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Services;

namespace NetworkMonitor.Api.Controllers;

[ApiController, Route("api/network-credentials")]
public sealed class NetworkCredentialsController(INetworkCredentialService service) : ControllerBase
{
    [HttpGet] public async Task<ActionResult<IReadOnlyList<NetworkCredentialResponse>>> List(CancellationToken token) => Ok(await service.ListAsync(token));
    [HttpPost] public async Task<ActionResult<NetworkCredentialResponse>> Create(CreateNetworkCredentialRequest request, CancellationToken token) { try { var result = await service.CreateAsync(request, token); return CreatedAtAction(nameof(List), new { id = result.Id }, result); } catch (ArgumentException ex) { return BadRequest(Problem(ex.Message)); } }
    [HttpPut("{id:int}")] public async Task<ActionResult<NetworkCredentialResponse>> Update(int id, UpdateNetworkCredentialRequest request, CancellationToken token) { try { return Ok(await service.UpdateAsync(id, request, token)); } catch (ArgumentException ex) { return BadRequest(Problem(ex.Message)); } catch (KeyNotFoundException) { return NotFound(); } }
    [HttpDelete("{id:int}")] public async Task<IActionResult> Delete(int id, CancellationToken token) { try { await service.DeleteAsync(id, token); return NoContent(); } catch (KeyNotFoundException) { return NotFound(); } }
    private static ProblemDetails Problem(string detail) => new() { Status = 400, Title = "Invalid credential request", Detail = detail };
}
