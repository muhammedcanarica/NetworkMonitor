using NetworkMonitor.Api.Models;
using NetworkMonitor.Api.Services;

namespace NetworkMonitor.Api.Tests.Services;

public sealed class DeviceStatusTrackerTests
{
    private const int FailureThreshold = 3;
    private const int RecoveryThreshold = 2;

    [Fact]
    public void Unknown_WithSuccessfulPing_ChangesToUp()
    {
        var tracker = new DeviceStatusTracker();

        var state = Apply(tracker, DeviceStatus.Unknown, isSuccess: true);

        Assert.Equal(DeviceStatus.Up, state.Status);
        Assert.Equal(0, state.ConsecutiveFailures);
        Assert.Equal(1, state.ConsecutiveSuccesses);
    }

    [Fact]
    public void Up_WithOneFailedPing_ChangesToWarning()
    {
        var tracker = new DeviceStatusTracker();

        var state = Apply(tracker, DeviceStatus.Up, isSuccess: false);

        Assert.Equal(DeviceStatus.Warning, state.Status);
        Assert.Equal(1, state.ConsecutiveFailures);
        Assert.Equal(0, state.ConsecutiveSuccesses);
    }

    [Fact]
    public void Unknown_AfterFailureThreshold_ChangesToDown()
    {
        var tracker = new DeviceStatusTracker();
        var status = DeviceStatus.Unknown;

        for (var attempt = 0; attempt < FailureThreshold; attempt++)
        {
            status = Apply(tracker, status, isSuccess: false).Status;
        }

        Assert.Equal(DeviceStatus.Down, status);
    }

    [Fact]
    public void Up_AfterFailureThreshold_ChangesThroughWarningToDown()
    {
        var tracker = new DeviceStatusTracker();

        var firstFailure = Apply(tracker, DeviceStatus.Up, isSuccess: false);
        var secondFailure = Apply(tracker, firstFailure.Status, isSuccess: false);
        var thirdFailure = Apply(tracker, secondFailure.Status, isSuccess: false);

        Assert.Equal(DeviceStatus.Warning, firstFailure.Status);
        Assert.Equal(DeviceStatus.Warning, secondFailure.Status);
        Assert.Equal(DeviceStatus.Down, thirdFailure.Status);
    }

    [Fact]
    public void Down_WithOneSuccessfulPing_RemainsDown()
    {
        var tracker = new DeviceStatusTracker();

        var state = Apply(tracker, DeviceStatus.Down, isSuccess: true);

        Assert.Equal(DeviceStatus.Down, state.Status);
        Assert.Equal(1, state.ConsecutiveSuccesses);
    }

    [Fact]
    public void Down_AfterRecoveryThreshold_ChangesToUp()
    {
        var tracker = new DeviceStatusTracker();
        var status = DeviceStatus.Down;

        for (var attempt = 0; attempt < RecoveryThreshold; attempt++)
        {
            status = Apply(tracker, status, isSuccess: true).Status;
        }

        Assert.Equal(DeviceStatus.Up, status);
    }

    [Fact]
    public void SuccessfulPing_ResetsFailureCounter()
    {
        var tracker = new DeviceStatusTracker();
        var warning = Apply(tracker, DeviceStatus.Up, isSuccess: false);

        var recovered = Apply(tracker, warning.Status, isSuccess: true);

        Assert.Equal(0, recovered.ConsecutiveFailures);
        Assert.Equal(1, recovered.ConsecutiveSuccesses);
    }

    [Fact]
    public void FailedPing_ResetsRecoveryCounter()
    {
        var tracker = new DeviceStatusTracker();
        var recovering = Apply(tracker, DeviceStatus.Down, isSuccess: true);

        var failed = Apply(tracker, recovering.Status, isSuccess: false);

        Assert.Equal(DeviceStatus.Down, failed.Status);
        Assert.Equal(1, failed.ConsecutiveFailures);
        Assert.Equal(0, failed.ConsecutiveSuccesses);
    }

    [Fact]
    public void Warning_AfterRecoveryThreshold_ChangesToUp()
    {
        var tracker = new DeviceStatusTracker();
        var status = DeviceStatus.Warning;

        for (var attempt = 0; attempt < RecoveryThreshold; attempt++)
        {
            status = Apply(tracker, status, isSuccess: true).Status;
        }

        Assert.Equal(DeviceStatus.Up, status);
    }

    private static DeviceMonitoringState Apply(
        DeviceStatusTracker tracker,
        DeviceStatus currentStatus,
        bool isSuccess)
    {
        return tracker.ApplyResult(
            deviceId: 1,
            currentStatus,
            isSuccess,
            FailureThreshold,
            RecoveryThreshold);
    }
}
