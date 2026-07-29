using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NetworkMonitor.Api.Data;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Controllers;

[ApiController]
[Route("api/devices")]
public sealed class DevicesController(NetworkMonitorDbContext dbContext) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DeviceResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DeviceResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var devices = await dbContext.Devices
            .AsNoTracking()
            .OrderBy(device => device.Id)
            .Select(device => new DeviceResponse(
                device.Id,
                device.Name,
                device.IpAddress,
                device.Description,
                device.Status,
                device.LastSeenAt,
                device.IsMonitoringEnabled,
                device.CreatedAt,
                device.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Ok(devices);
    }

    [HttpGet("{id:int}", Name = nameof(GetById))]
    [ProducesResponseType<DeviceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeviceResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var device = await dbContext.Devices
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        return device is null
            ? NotFound()
            : Ok(DeviceResponse.FromDevice(device));
    }

    [HttpPost]
    [ProducesResponseType<DeviceResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DeviceResponse>> Create(
        CreateDeviceRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedIpAddress = NormalizeIpAddress(request.IpAddress);

        if (await IpAddressExists(normalizedIpAddress, null, cancellationToken))
        {
            return DuplicateIpAddress(normalizedIpAddress);
        }

        var now = DateTimeOffset.UtcNow;
        var device = new Device
        {
            Name = request.Name.Trim(),
            IpAddress = normalizedIpAddress,
            Description = NormalizeDescription(request.Description),
            Status = DeviceStatus.Unknown,
            LastSeenAt = null,
            IsMonitoringEnabled = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.Devices.Add(device);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return DuplicateIpAddress(normalizedIpAddress);
        }

        var response = DeviceResponse.FromDevice(device);
        return CreatedAtAction(nameof(GetById), new { id = device.Id }, response);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<DeviceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DeviceResponse>> Update(
        int id,
        UpdateDeviceRequest request,
        CancellationToken cancellationToken)
    {
        var device = await dbContext.Devices.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (device is null)
        {
            return NotFound();
        }

        var normalizedIpAddress = NormalizeIpAddress(request.IpAddress);

        if (await IpAddressExists(normalizedIpAddress, id, cancellationToken))
        {
            return DuplicateIpAddress(normalizedIpAddress);
        }

        device.Name = request.Name.Trim();
        device.IpAddress = normalizedIpAddress;
        device.Description = NormalizeDescription(request.Description);
        device.IsMonitoringEnabled = request.IsMonitoringEnabled!.Value;
        device.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return DuplicateIpAddress(normalizedIpAddress);
        }

        return Ok(DeviceResponse.FromDevice(device));
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var device = await dbContext.Devices.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (device is null)
        {
            return NotFound();
        }

        dbContext.Devices.Remove(device);
        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private Task<bool> IpAddressExists(
        string ipAddress,
        int? excludedDeviceId,
        CancellationToken cancellationToken)
    {
        return dbContext.Devices.AnyAsync(
            device => device.IpAddress == ipAddress
                && (!excludedDeviceId.HasValue || device.Id != excludedDeviceId.Value),
            cancellationToken);
    }

    private ConflictObjectResult DuplicateIpAddress(string ipAddress)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Duplicate IP address",
            Detail = $"A device with IP address '{ipAddress}' already exists."
        };
        problem.Extensions["ipAddress"] = ipAddress;

        return Conflict(problem);
    }

    private static string NormalizeIpAddress(string ipAddress)
    {
        return IPAddress.Parse(ipAddress.Trim()).ToString();
    }

    private static string? NormalizeDescription(string? description)
    {
        return string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqliteException { SqliteErrorCode: 19 };
    }
}
