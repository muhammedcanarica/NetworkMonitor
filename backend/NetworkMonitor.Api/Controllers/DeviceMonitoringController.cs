using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetworkMonitor.Api.Data;
using NetworkMonitor.Api.Dtos;

namespace NetworkMonitor.Api.Controllers;

[ApiController]
[Route("api/devices/{deviceId:int}")]
public sealed class DeviceMonitoringController(NetworkMonitorDbContext dbContext) : ControllerBase
{
    private const int DefaultLimit = 100;
    private const int MaximumLimit = 1000;

    [HttpGet("checks")]
    [ProducesResponseType<IReadOnlyList<CheckResultResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<CheckResultResponse>>> GetChecks(
        int deviceId,
        CancellationToken cancellationToken,
        [FromQuery] int limit = DefaultLimit)
    {
        if (limit is < 1 or > MaximumLimit)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid history limit",
                Detail = $"Limit must be between 1 and {MaximumLimit}."
            });
        }

        if (!await DeviceExists(deviceId, cancellationToken))
        {
            return NotFound();
        }

        var results = await dbContext.CheckResults
            .AsNoTracking()
            .Where(result => result.DeviceId == deviceId)
            .OrderByDescending(result => result.CheckedAt)
            .ThenByDescending(result => result.Id)
            .Take(limit)
            .Select(result => new CheckResultResponse(
                result.Id,
                result.DeviceId,
                result.CheckedAt,
                result.IsSuccess,
                result.LatencyMs,
                result.DeviceStatus,
                result.FailureReason))
            .ToListAsync(cancellationToken);

        return Ok(results);
    }

    [HttpGet("summary")]
    [ProducesResponseType<DeviceMonitoringSummaryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeviceMonitoringSummaryResponse>> GetSummary(
        int deviceId,
        CancellationToken cancellationToken)
    {
        if (!await DeviceExists(deviceId, cancellationToken))
        {
            return NotFound();
        }

        var cutoff = DateTimeOffset.UtcNow.AddHours(-24);
        var aggregate = await dbContext.CheckResults
            .AsNoTracking()
            .Where(result => result.DeviceId == deviceId && result.CheckedAt >= cutoff)
            .GroupBy(_ => 1)
            .Select(group => new SummaryAggregate(
                group.Count(),
                group.Count(result => result.IsSuccess),
                group.Count(result => !result.IsSuccess),
                group.Where(result => result.IsSuccess && result.LatencyMs.HasValue)
                    .Average(result => (double?)result.LatencyMs),
                group.Where(result => result.IsSuccess && result.LatencyMs.HasValue)
                    .Min(result => result.LatencyMs),
                group.Where(result => result.IsSuccess && result.LatencyMs.HasValue)
                    .Max(result => result.LatencyMs)))
            .SingleOrDefaultAsync(cancellationToken);

        if (aggregate is null)
        {
            return Ok(new DeviceMonitoringSummaryResponse(0, 0, 0, 0, null, null, null));
        }

        var uptimePercentage = aggregate.TotalChecks == 0
            ? 0
            : Math.Round(aggregate.SuccessfulChecks * 100d / aggregate.TotalChecks, 2);

        return Ok(new DeviceMonitoringSummaryResponse(
            aggregate.TotalChecks,
            aggregate.SuccessfulChecks,
            aggregate.FailedChecks,
            uptimePercentage,
            aggregate.AverageLatencyMs,
            aggregate.MinLatencyMs,
            aggregate.MaxLatencyMs));
    }

    private Task<bool> DeviceExists(int deviceId, CancellationToken cancellationToken)
    {
        return dbContext.Devices.AnyAsync(device => device.Id == deviceId, cancellationToken);
    }

    private sealed record SummaryAggregate(
        int TotalChecks,
        int SuccessfulChecks,
        int FailedChecks,
        double? AverageLatencyMs,
        long? MinLatencyMs,
        long? MaxLatencyMs);
}
