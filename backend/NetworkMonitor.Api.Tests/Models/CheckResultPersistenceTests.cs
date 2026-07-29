using Microsoft.EntityFrameworkCore;
using NetworkMonitor.Api.Models;
using NetworkMonitor.Api.Tests.Infrastructure;

namespace NetworkMonitor.Api.Tests.Models;

public sealed class CheckResultPersistenceTests
{
    [Fact]
    public async Task SuccessfulCheckResult_IsSavedWithExpectedFields()
    {
        await using var database = await TestDatabase.CreateAsync();
        var device = await database.AddDeviceAsync();
        var checkedAt = DateTimeOffset.UtcNow;
        var checkResult = CheckResult.Create(
            device.Id,
            checkedAt,
            PingCheckResult.Succeeded(12),
            DeviceStatus.Up);

        database.Context.CheckResults.Add(checkResult);
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();

        var saved = await database.Context.CheckResults.SingleAsync();
        Assert.True(saved.IsSuccess);
        Assert.Equal(12, saved.LatencyMs);
        Assert.Equal(DeviceStatus.Up, saved.DeviceStatus);
        Assert.Null(saved.FailureReason);
        Assert.Equal(checkedAt.ToUnixTimeMilliseconds(), saved.CheckedAt.ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task FailedCheckResult_AlwaysStoresNullLatencyAndControlledReason()
    {
        await using var database = await TestDatabase.CreateAsync();
        var device = await database.AddDeviceAsync();
        var malformedFailure = new PingCheckResult(false, 99, "raw exception details");
        var checkResult = CheckResult.Create(
            device.Id,
            DateTimeOffset.UtcNow,
            malformedFailure,
            DeviceStatus.Warning);

        database.Context.CheckResults.Add(checkResult);
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();

        var saved = await database.Context.CheckResults.SingleAsync();
        Assert.False(saved.IsSuccess);
        Assert.Null(saved.LatencyMs);
        Assert.Equal(PingFailureReasons.Unknown, saved.FailureReason);
    }

    [Fact]
    public async Task DeletingDevice_CascadeDeletesCheckHistory()
    {
        await using var database = await TestDatabase.CreateAsync();
        var device = await database.AddDeviceAsync();
        database.Context.CheckResults.Add(CheckResult.Create(
            device.Id,
            DateTimeOffset.UtcNow,
            PingCheckResult.Succeeded(5),
            DeviceStatus.Up));
        await database.Context.SaveChangesAsync();

        database.Context.Devices.Remove(device);
        await database.Context.SaveChangesAsync();

        Assert.False(await database.Context.CheckResults.AnyAsync());
    }
}
