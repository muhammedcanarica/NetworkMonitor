using Microsoft.AspNetCore.Mvc;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Services;

namespace NetworkMonitor.Api.Controllers;

[ApiController]
[Route("api/tools/config-backup")]
public sealed class ConfigBackupController(IConfigBackupService configBackupService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<ConfigBackupResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<ConfigBackupResponse>> GetRunningConfiguration(
        ConfigBackupRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await configBackupService.GetRunningConfigurationAsync(request, cancellationToken));
        }
        catch (ConfigBackupValidationException exception)
        {
            return BadRequest(CreateProblem(
                StatusCodes.Status400BadRequest,
                "Invalid configuration backup request",
                exception.Message));
        }
        catch (ConfigBackupOperationException exception)
        {
            var status = exception.Kind switch
            {
                ConfigBackupErrorKind.Authentication => StatusCodes.Status401Unauthorized,
                ConfigBackupErrorKind.ConnectionTimeout or ConfigBackupErrorKind.CommandTimeout => StatusCodes.Status504GatewayTimeout,
                _ => StatusCodes.Status502BadGateway
            };
            var title = exception.Kind switch
            {
                ConfigBackupErrorKind.Authentication => "SSH authentication failed",
                ConfigBackupErrorKind.ConnectionTimeout => "SSH connection timed out",
                ConfigBackupErrorKind.CommandTimeout => "SSH command timed out",
                _ => "Configuration backup failed"
            };
            return StatusCode(status, CreateProblem(status, title, exception.Message));
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
