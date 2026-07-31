using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetworkMonitor.Api.Data;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Controllers;

[ApiController]
[Route("api/incidents")]
public sealed class IncidentsController(NetworkMonitorDbContext dbContext) : ControllerBase
{
    private const int MaximumResults = 200;

    [HttpGet]
    public Task<ActionResult<IReadOnlyList<IncidentResponse>>> GetAll([FromQuery] IncidentStatus? status, CancellationToken cancellationToken) =>
        ListAsync(null, status, cancellationToken);

    [HttpGet("device/{deviceId:int}")]
    public async Task<ActionResult<IReadOnlyList<IncidentResponse>>> GetByDevice(int deviceId, [FromQuery] IncidentStatus? status, CancellationToken cancellationToken)
    {
        if (!await dbContext.Devices.AnyAsync(device => device.Id == deviceId, cancellationToken)) return NotFound();
        return await ListAsync(deviceId, status, cancellationToken);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<IncidentResponse>> GetById(long id, CancellationToken cancellationToken)
    {
        var incident = await BuildQuery()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return incident is null ? NotFound() : Ok(ToResponse(incident, DateTimeOffset.UtcNow));
    }

    private async Task<ActionResult<IReadOnlyList<IncidentResponse>>> ListAsync(int? deviceId, IncidentStatus? status, CancellationToken cancellationToken)
    {
        var query = BuildQuery();
        if (deviceId.HasValue) query = query.Where(item => item.DeviceId == deviceId.Value);
        if (status.HasValue) query = query.Where(item => item.Status == status.Value);
        var now = DateTimeOffset.UtcNow;
        var incidents = await query.OrderByDescending(item => item.StartedAt).Take(MaximumResults).ToListAsync(cancellationToken);
        return Ok(incidents.Select(item => ToResponse(item, now)).ToList());
    }

    private IQueryable<Incident> BuildQuery()
    {
        return dbContext.Incidents.AsNoTracking().Include(item => item.Device);
    }

    private static IncidentResponse ToResponse(Incident item, DateTimeOffset now) => new(
        item.Id, item.DeviceId, item.Device.Name, item.Device.IpAddress, item.Type, item.Status,
        item.Summary, item.StartedAt, item.ResolvedAt,
        (long)Math.Max(0, ((item.ResolvedAt ?? now) - item.StartedAt).TotalSeconds));
}
