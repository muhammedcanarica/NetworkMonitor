using Microsoft.AspNetCore.Mvc;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Services;

namespace NetworkMonitor.Api.Controllers;

[ApiController]
[Route("api/tools/config-backup")]
public sealed class ConfigBackupController(
    IConfigBackupService configBackupService,
    IConfigBackupStorageService configBackupStorageService,
    INetworkOperationCredentialResolver credentialResolver) : ControllerBase
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
            var credential = await credentialResolver.ResolveSshCredentialAsync(
                request.Username,
                request.Password,
                request.CredentialId,
                cancellationToken);
            var resolvedRequest = new ConfigBackupRequest
            {
                IpAddress = request.IpAddress,
                Port = request.Port,
                Username = credential.Username,
                Password = credential.Password,
                Vendor = request.Vendor
            };
            return Ok(await configBackupService.GetRunningConfigurationAsync(resolvedRequest, cancellationToken));
        }
        catch (NetworkOperationCredentialException exception)
        {
            return BadRequest(CreateProblem(
                StatusCodes.Status400BadRequest,
                "Invalid SSH credential",
                exception.Message));
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

    [HttpPost("/api/config-backups")]
    [ProducesResponseType<SaveConfigBackupResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    public Task<ActionResult<SaveConfigBackupResponse>> Save(
        SaveConfigBackupRequest request,
        CancellationToken cancellationToken)
    {
        return ExecuteStorage(() => configBackupStorageService.SaveAsync(request, cancellationToken));
    }

    [HttpGet("/api/config-backups")]
    [ProducesResponseType<IReadOnlyList<ConfigBackupListItemResponse>>(StatusCodes.Status200OK)]
    public Task<ActionResult<IReadOnlyList<ConfigBackupListItemResponse>>> List(
        [FromQuery] int? deviceId,
        CancellationToken cancellationToken)
    {
        return ExecuteStorage(() => configBackupStorageService.ListAsync(deviceId, cancellationToken));
    }

    [HttpGet("/api/config-backups/{id:int}")]
    [ProducesResponseType<ConfigBackupDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ActionResult<ConfigBackupDetailResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        return ExecuteStorage(() => configBackupStorageService.GetByIdAsync(id, cancellationToken));
    }

    [HttpGet("/api/config-backups/device/{deviceId:int}")]
    [ProducesResponseType<IReadOnlyList<ConfigBackupListItemResponse>>(StatusCodes.Status200OK)]
    public Task<ActionResult<IReadOnlyList<ConfigBackupListItemResponse>>> GetByDevice(
        int deviceId,
        CancellationToken cancellationToken)
    {
        return ExecuteStorage(() => configBackupStorageService.ListAsync(deviceId, cancellationToken));
    }

    [HttpGet("/api/config-backups/compare")]
    [ProducesResponseType<ConfigBackupComparisonResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    public Task<ActionResult<ConfigBackupComparisonResponse>> Compare(
        [FromQuery] int fromId,
        [FromQuery] int toId,
        CancellationToken cancellationToken)
    {
        return ExecuteStorage(() => configBackupStorageService.CompareAsync(fromId, toId, cancellationToken));
    }

    private async Task<ActionResult<T>> ExecuteStorage<T>(Func<Task<T>> operation)
    {
        try
        {
            return Ok(await operation());
        }
        catch (ConfigBackupStorageValidationException exception)
        {
            return BadRequest(CreateProblem(
                StatusCodes.Status400BadRequest,
                "Invalid configuration backup request",
                exception.Message));
        }
        catch (ConfigBackupSizeLimitException exception)
        {
            return StatusCode(
                StatusCodes.Status413PayloadTooLarge,
                CreateProblem(
                    StatusCodes.Status413PayloadTooLarge,
                    "Configuration backup exceeds a safe limit",
                    exception.Message));
        }
        catch (ConfigBackupNotFoundException exception)
        {
            return NotFound(CreateProblem(
                StatusCodes.Status404NotFound,
                "Configuration backup was not found",
                exception.Message));
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
