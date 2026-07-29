using Microsoft.AspNetCore.Mvc;
using NetworkMonitor.Api.Controllers;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Models;
using NetworkMonitor.Api.Tests.Infrastructure;

namespace NetworkMonitor.Api.Tests.Controllers;

public sealed class DeviceMonitoringControllerTests
{
    [Fact]
    public async Task GetChecks_DefaultLimitReturnsNewest100Results()
    {
        await using var database = await TestDatabase.CreateAsync();
        var device = await database.AddDeviceAsync();
        var start = DateTimeOffset.UtcNow.AddMinutes(-105);

        for (var index = 0; index < 105; index++)
        {
            database.Context.CheckResults.Add(CheckResult.Create(
                device.Id,
                start.AddMinutes(index),
                PingCheckResult.Succeeded(index),
                DeviceStatus.Up));
        }

        await database.Context.SaveChangesAsync();
        var controller = new DeviceMonitoringController(database.Context);

        var action = await controller.GetChecks(device.Id, CancellationToken.None);

        var results = GetOkValue(action);
        Assert.Equal(100, results.Count);
        Assert.True(results[0].CheckedAt > results[^1].CheckedAt);
    }

    [Fact]
    public async Task GetChecks_AppliesRequestedLimitAndNewestFirstOrder()
    {
        await using var database = await TestDatabase.CreateAsync();
        var device = await database.AddDeviceAsync();
        var now = DateTimeOffset.UtcNow;
        database.Context.CheckResults.AddRange(
            CheckResult.Create(device.Id, now.AddMinutes(-2), PingCheckResult.Succeeded(1), DeviceStatus.Up),
            CheckResult.Create(device.Id, now.AddMinutes(-1), PingCheckResult.Succeeded(2), DeviceStatus.Up),
            CheckResult.Create(device.Id, now, PingCheckResult.Succeeded(3), DeviceStatus.Up));
        await database.Context.SaveChangesAsync();
        var controller = new DeviceMonitoringController(database.Context);

        var action = await controller.GetChecks(device.Id, CancellationToken.None, limit: 2);

        var results = GetOkValue(action);
        Assert.Equal(2, results.Count);
        Assert.Equal(3, results[0].LatencyMs);
        Assert.Equal(2, results[1].LatencyMs);
    }

    [Fact]
    public async Task GetChecks_RejectsLimitAboveMaximum()
    {
        await using var database = await TestDatabase.CreateAsync();
        var device = await database.AddDeviceAsync();
        var controller = new DeviceMonitoringController(database.Context);

        var action = await controller.GetChecks(device.Id, CancellationToken.None, limit: 1001);

        var badRequest = Assert.IsType<BadRequestObjectResult>(action.Result);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task GetChecks_RejectsLimitBelowMinimum()
    {
        await using var database = await TestDatabase.CreateAsync();
        var device = await database.AddDeviceAsync();
        var controller = new DeviceMonitoringController(database.Context);

        var action = await controller.GetChecks(device.Id, CancellationToken.None, limit: 0);

        var badRequest = Assert.IsType<BadRequestObjectResult>(action.Result);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task GetChecks_ForMissingDevice_ReturnsNotFound()
    {
        await using var database = await TestDatabase.CreateAsync();
        var controller = new DeviceMonitoringController(database.Context);

        var action = await controller.GetChecks(999, CancellationToken.None);

        Assert.IsType<NotFoundResult>(action.Result);
    }

    [Fact]
    public async Task GetSummary_CalculatesCorrectUptimePercentage()
    {
        await using var database = await TestDatabase.CreateAsync();
        var device = await database.AddDeviceAsync();
        var now = DateTimeOffset.UtcNow;
        database.Context.CheckResults.AddRange(
            CheckResult.Create(device.Id, now.AddMinutes(-4), PingCheckResult.Succeeded(10), DeviceStatus.Up),
            CheckResult.Create(device.Id, now.AddMinutes(-3), PingCheckResult.Succeeded(10), DeviceStatus.Up),
            CheckResult.Create(device.Id, now.AddMinutes(-2), PingCheckResult.Succeeded(10), DeviceStatus.Up),
            CheckResult.Create(device.Id, now.AddMinutes(-1), PingCheckResult.Failed(PingFailureReasons.Timeout), DeviceStatus.Warning));
        await database.Context.SaveChangesAsync();
        var controller = new DeviceMonitoringController(database.Context);

        var action = await controller.GetSummary(device.Id, CancellationToken.None);

        var summary = GetOkValue(action);
        Assert.Equal(4, summary.TotalChecks);
        Assert.Equal(3, summary.SuccessfulChecks);
        Assert.Equal(1, summary.FailedChecks);
        Assert.Equal(75, summary.UptimePercentage);
    }

    [Fact]
    public async Task GetSummary_CalculatesLatencyFromSuccessfulChecksOnly()
    {
        await using var database = await TestDatabase.CreateAsync();
        var device = await database.AddDeviceAsync();
        var now = DateTimeOffset.UtcNow;
        database.Context.CheckResults.AddRange(
            CheckResult.Create(device.Id, now.AddMinutes(-3), PingCheckResult.Succeeded(10), DeviceStatus.Up),
            CheckResult.Create(device.Id, now.AddMinutes(-2), PingCheckResult.Succeeded(20), DeviceStatus.Up),
            CheckResult.Create(device.Id, now.AddMinutes(-1), PingCheckResult.Failed(PingFailureReasons.Timeout), DeviceStatus.Warning));
        await database.Context.SaveChangesAsync();
        var controller = new DeviceMonitoringController(database.Context);

        var action = await controller.GetSummary(device.Id, CancellationToken.None);

        var summary = GetOkValue(action);
        Assert.Equal(15, summary.AverageLatencyMs);
        Assert.Equal(10, summary.MinLatencyMs);
        Assert.Equal(20, summary.MaxLatencyMs);
    }

    [Fact]
    public async Task GetSummary_WithNoChecks_ReturnsZeroValuesWithoutError()
    {
        await using var database = await TestDatabase.CreateAsync();
        var device = await database.AddDeviceAsync();
        var controller = new DeviceMonitoringController(database.Context);

        var action = await controller.GetSummary(device.Id, CancellationToken.None);

        var summary = GetOkValue(action);
        Assert.Equal(0, summary.TotalChecks);
        Assert.Equal(0, summary.UptimePercentage);
        Assert.Null(summary.AverageLatencyMs);
        Assert.Null(summary.MinLatencyMs);
        Assert.Null(summary.MaxLatencyMs);
    }

    [Fact]
    public async Task GetSummary_ExcludesChecksOlderThan24Hours()
    {
        await using var database = await TestDatabase.CreateAsync();
        var device = await database.AddDeviceAsync();
        var now = DateTimeOffset.UtcNow;
        database.Context.CheckResults.AddRange(
            CheckResult.Create(
                device.Id,
                now.AddHours(-25),
                PingCheckResult.Failed(PingFailureReasons.Timeout),
                DeviceStatus.Down),
            CheckResult.Create(
                device.Id,
                now.AddMinutes(-1),
                PingCheckResult.Succeeded(5),
                DeviceStatus.Up));
        await database.Context.SaveChangesAsync();
        var controller = new DeviceMonitoringController(database.Context);

        var action = await controller.GetSummary(device.Id, CancellationToken.None);

        var summary = GetOkValue(action);
        Assert.Equal(1, summary.TotalChecks);
        Assert.Equal(1, summary.SuccessfulChecks);
        Assert.Equal(100, summary.UptimePercentage);
    }

    private static T GetOkValue<T>(ActionResult<T> action)
    {
        var ok = Assert.IsType<OkObjectResult>(action.Result);
        return Assert.IsAssignableFrom<T>(ok.Value);
    }
}
