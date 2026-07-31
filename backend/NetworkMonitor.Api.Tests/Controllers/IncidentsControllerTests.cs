using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetworkMonitor.Api.Controllers;
using NetworkMonitor.Api.Models;
using NetworkMonitor.Api.Tests.Infrastructure;

namespace NetworkMonitor.Api.Tests.Controllers;

public sealed class IncidentsControllerTests
{
    [Fact]
    public async Task GetAllAndGetByDevice_FilterIncidentsAndReturnDuration()
    {
        await using var database = await TestDatabase.CreateAsync();
        var device = await database.AddDeviceAsync("Core", "192.0.2.1");
        database.Context.Incidents.AddRange(
            CreateIncident(device.Id, IncidentStatus.Open),
            CreateIncident(device.Id, IncidentStatus.Resolved));
        await database.Context.SaveChangesAsync();
        var controller = new IncidentsController(database.Context);

        var openAction = await controller.GetAll(IncidentStatus.Open, CancellationToken.None);
        var open = Assert.IsType<OkObjectResult>(openAction.Result).Value as IReadOnlyList<NetworkMonitor.Api.Dtos.IncidentResponse>;
        Assert.Single(open!);
        Assert.Equal(IncidentStatus.Open, open![0].Status);
        Assert.True(open[0].DurationSeconds >= 0);

        var deviceAction = await controller.GetByDevice(device.Id, IncidentStatus.Resolved, CancellationToken.None);
        var resolved = Assert.IsType<OkObjectResult>(deviceAction.Result).Value as IReadOnlyList<NetworkMonitor.Api.Dtos.IncidentResponse>;
        Assert.Single(resolved!);
        Assert.Equal(IncidentStatus.Resolved, resolved![0].Status);
    }

    [Fact]
    public async Task GetByDevice_ReturnsNotFoundForInvalidDevice()
    {
        await using var database = await TestDatabase.CreateAsync();
        var action = await new IncidentsController(database.Context).GetByDevice(999, null, CancellationToken.None);
        Assert.IsType<NotFoundResult>(action.Result);
    }

    private static Incident CreateIncident(int deviceId, IncidentStatus status)
    {
        var started = DateTimeOffset.UtcNow.AddMinutes(-5);
        return new Incident
        {
            DeviceId = deviceId, Type = IncidentType.DeviceUnreachable, Status = status,
            Summary = "Device became unreachable", StartedAt = started, CreatedAt = started, UpdatedAt = started,
            ResolvedAt = status == IncidentStatus.Resolved ? DateTimeOffset.UtcNow : null
        };
    }
}
