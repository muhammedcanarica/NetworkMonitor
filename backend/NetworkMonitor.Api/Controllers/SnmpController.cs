using Microsoft.AspNetCore.Mvc;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Services;

namespace NetworkMonitor.Api.Controllers;

[ApiController]
[Route("api/tools/snmp")]
public sealed class SnmpController(
    ISnmpService snmpService,
    INetworkOperationCredentialResolver credentialResolver,
    ILogger<SnmpController> logger) : ControllerBase
{
    [HttpPost("system-info")]
    public Task<ActionResult<SnmpSystemInfoResponse>> GetSystemInfo(
        SnmpSystemInfoRequest request,
        CancellationToken cancellationToken)
    {
        return Execute(
            async () => await snmpService.GetSystemInfoAsync(
                request.IpAddress,
                await credentialResolver.ResolveSnmpCommunityAsync(request.Community, request.CredentialId, cancellationToken),
                request.TimeoutMilliseconds,
                cancellationToken));
    }

    [HttpPost("get")]
    public Task<ActionResult<SnmpValueResponse>> Get(
        SnmpGetRequest request,
        CancellationToken cancellationToken)
    {
        return Execute(
            async () => await snmpService.GetAsync(
                request.IpAddress,
                await credentialResolver.ResolveSnmpCommunityAsync(request.Community, request.CredentialId, cancellationToken),
                request.Oid,
                request.TimeoutMilliseconds,
                cancellationToken));
    }

    [HttpPost("walk")]
    public Task<ActionResult<SnmpWalkResponse>> Walk(
        SnmpWalkRequest request,
        CancellationToken cancellationToken)
    {
        return Execute(
            async () => await snmpService.WalkAsync(
                request.IpAddress,
                await credentialResolver.ResolveSnmpCommunityAsync(request.Community, request.CredentialId, cancellationToken),
                request.RootOid,
                request.TimeoutMilliseconds,
                cancellationToken));
    }

    [HttpPost("interfaces")]
    public Task<ActionResult<IReadOnlyList<SnmpInterfaceResponse>>> GetInterfaces(
        SnmpInterfacesRequest request,
        CancellationToken cancellationToken)
    {
        return Execute(
            async () => await snmpService.GetInterfacesAsync(
                request.IpAddress,
                await credentialResolver.ResolveSnmpCommunityAsync(request.Community, request.CredentialId, cancellationToken),
                request.TimeoutMilliseconds,
                cancellationToken));
    }

    private async Task<ActionResult<T>> Execute<T>(Func<Task<T>> operation)
    {
        try
        {
            return Ok(await operation());
        }
        catch (SnmpValidationException exception)
        {
            return BadRequest(CreateProblem(
                StatusCodes.Status400BadRequest,
                "Invalid SNMP request",
                exception.Message));
        }
        catch (NetworkOperationCredentialException exception)
        {
            return BadRequest(CreateProblem(
                StatusCodes.Status400BadRequest,
                "Invalid SNMP credential",
                exception.Message));
        }
        catch (SnmpOperationException exception)
        {
            var status = exception.Kind == SnmpErrorKind.Timeout
                ? StatusCodes.Status504GatewayTimeout
                : StatusCodes.Status502BadGateway;
            return StatusCode(status, CreateProblem(
                status,
                exception.Kind == SnmpErrorKind.Timeout
                    ? "SNMP request timed out"
                    : "SNMP request failed",
                exception.Message));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                "SNMP API operation failed with {ErrorType}.",
                exception.GetType().Name);
            return StatusCode(
                StatusCodes.Status502BadGateway,
                CreateProblem(
                    StatusCodes.Status502BadGateway,
                    "SNMP request failed",
                    "The SNMP request failed unexpectedly."));
        }
    }

    private static ProblemDetails CreateProblem(int status, string title, string detail)
    {
        return new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail
        };
    }
}
