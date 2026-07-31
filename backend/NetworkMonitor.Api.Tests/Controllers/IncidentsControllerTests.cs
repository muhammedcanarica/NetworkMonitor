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
        Assert.Null(open[0].InterfaceIndex);
        Assert.Null(open[0].Direction);
        Assert.True(open[0].DurationSeconds >= 0);

        var deviceAction = await controller.GetByDevice(device.Id, IncidentStatus.Resolved, CancellationToken.None);
        var resolved = Assert.IsType<OkObjectResult>(deviceAction.Result).Value as IReadOnlyList<NetworkMonitor.Api.Dtos.IncidentResponse>;
        Assert.Single(resolved!);
        Assert.Equal(IncidentStatus.Resolved, resolved![0].Status);
    }

    [Fact]
    public async Task GetById_ReturnsBandwidthInterfaceContext()
    {
        await using var database = await TestDatabase.CreateAsync();
        var device = await database.AddDeviceAsync("Core", "192.0.2.1");
        var monitored = new SnmpMonitoredInterface
        {
            InterfaceIndex = 7, InterfaceName = "Gi0/7", IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow,
            Profile = new SnmpMonitoringProfile { DeviceId = device.Id, Credential = new NetworkCredential { Name = "SNMP", Type = NetworkCredentialType.SnmpV2Community, ProtectedSecret = "x", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow }, IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow }
        };
        var incident = new Incident { DeviceId = device.Id, SnmpMonitoredInterface = monitored, Type = IncidentType.InterfaceInboundBandwidthHigh, Status = IncidentStatus.Open, Summary = "Inbound bandwidth threshold exceeded on interface 7", ThresholdBitsPerSecond = 100_000_000, ObservedBitsPerSecond = 120_000_000, StartedAt = DateTimeOffset.UtcNow, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        database.Context.Add(incident);
        await database.Context.SaveChangesAsync();

        var action = await new IncidentsController(database.Context).GetById(incident.Id, CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(action.Result).Value as NetworkMonitor.Api.Dtos.IncidentResponse;
        Assert.NotNull(response);
        Assert.Equal(7, response!.InterfaceIndex);
        Assert.Equal("Gi0/7", response.InterfaceName);
        Assert.Equal(BandwidthDirection.Inbound, response.Direction);
        Assert.Equal(100_000_000, response.ThresholdBitsPerSecond);
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
