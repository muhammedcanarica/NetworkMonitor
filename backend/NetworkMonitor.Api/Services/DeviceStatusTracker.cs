using System.Collections.Concurrent;
using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Services;

public sealed class DeviceStatusTracker
{
    private readonly ConcurrentDictionary<int, MonitoringCounters> _counters = new();

    public DeviceMonitoringState ApplyResult(
        int deviceId,
        DeviceStatus currentStatus,
        bool isSuccess,
        int failureThreshold,
        int recoveryThreshold)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(failureThreshold);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(recoveryThreshold);

        var counters = _counters.AddOrUpdate(
            deviceId,
            _ => NextCounters(default, isSuccess),
            (_, current) => NextCounters(current, isSuccess));

        var nextStatus = ResolveStatus(
            currentStatus,
            isSuccess,
            counters,
            failureThreshold,
            recoveryThreshold);

        return new DeviceMonitoringState(
            nextStatus,
            counters.ConsecutiveFailures,
            counters.ConsecutiveSuccesses);
    }

    public void RetainOnly(IEnumerable<int> activeDeviceIds)
    {
        var activeIds = activeDeviceIds.ToHashSet();

        foreach (var deviceId in _counters.Keys)
        {
            if (!activeIds.Contains(deviceId))
            {
                _counters.TryRemove(deviceId, out _);
            }
        }
    }

    private static MonitoringCounters NextCounters(MonitoringCounters current, bool isSuccess)
    {
        return isSuccess
            ? new MonitoringCounters(0, Increment(current.ConsecutiveSuccesses))
            : new MonitoringCounters(Increment(current.ConsecutiveFailures), 0);
    }

    private static DeviceStatus ResolveStatus(
        DeviceStatus currentStatus,
        bool isSuccess,
        MonitoringCounters counters,
        int failureThreshold,
        int recoveryThreshold)
    {
        if (isSuccess)
        {
            return currentStatus switch
            {
                DeviceStatus.Unknown => DeviceStatus.Up,
                DeviceStatus.Warning when counters.ConsecutiveSuccesses >= recoveryThreshold => DeviceStatus.Up,
                DeviceStatus.Down when counters.ConsecutiveSuccesses >= recoveryThreshold => DeviceStatus.Up,
                _ => currentStatus
            };
        }

        return currentStatus switch
        {
            DeviceStatus.Unknown when counters.ConsecutiveFailures >= failureThreshold => DeviceStatus.Down,
            DeviceStatus.Up when counters.ConsecutiveFailures >= failureThreshold => DeviceStatus.Down,
            DeviceStatus.Up => DeviceStatus.Warning,
            DeviceStatus.Warning when counters.ConsecutiveFailures >= failureThreshold => DeviceStatus.Down,
            _ => currentStatus
        };
    }

    private static int Increment(int value)
    {
        return value == int.MaxValue ? int.MaxValue : value + 1;
    }

    private readonly record struct MonitoringCounters(
        int ConsecutiveFailures,
        int ConsecutiveSuccesses);
}
