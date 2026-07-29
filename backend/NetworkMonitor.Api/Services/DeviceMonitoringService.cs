using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetworkMonitor.Api.Configuration;
using NetworkMonitor.Api.Data;
using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Services;

public sealed class DeviceMonitoringService(
    IServiceScopeFactory scopeFactory,
    IPingService pingService,
    DeviceStatusTracker statusTracker,
    IOptions<MonitoringOptions> monitoringOptions,
    ILogger<DeviceMonitoringService> logger) : BackgroundService
{
    private readonly MonitoringOptions _options = monitoringOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunMonitoringCycle(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Device monitoring cycle failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.IntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task RunMonitoringCycle(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NetworkMonitorDbContext>();

        var targets = await dbContext.Devices
            .AsNoTracking()
            .Where(device => device.IsMonitoringEnabled)
            .Select(device => new MonitoringTarget(device.Id, device.IpAddress))
            .ToListAsync(cancellationToken);

        statusTracker.RetainOnly(targets.Select(target => target.DeviceId));

        if (targets.Count == 0)
        {
            return;
        }

        var outcomes = new ConcurrentBag<PingOutcome>();
        var parallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = _options.MaxConcurrentPings
        };

        await Parallel.ForEachAsync(targets, parallelOptions, async (target, token) =>
        {
            var result = await pingService.CheckAsync(
                target.IpAddress,
                _options.PingTimeoutMilliseconds,
                token);

            outcomes.Add(new PingOutcome(target, result, DateTimeOffset.UtcNow));
        });

        var targetIds = outcomes.Select(outcome => outcome.Target.DeviceId).ToArray();
        var devices = await dbContext.Devices
            .Where(device => targetIds.Contains(device.Id) && device.IsMonitoringEnabled)
            .ToDictionaryAsync(device => device.Id, cancellationToken);

        foreach (var outcome in outcomes)
        {
            if (!devices.TryGetValue(outcome.Target.DeviceId, out var device)
                || device.IpAddress != outcome.Target.IpAddress)
            {
                continue;
            }

            ApplyOutcome(device, outcome);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private void ApplyOutcome(Device device, PingOutcome outcome)
    {
        var previousStatus = device.Status;
        var state = statusTracker.ApplyResult(
            device.Id,
            previousStatus,
            outcome.Result.Success,
            _options.FailureThreshold,
            _options.RecoveryThreshold);

        device.Status = state.Status;
        device.LastCheckedAt = outcome.CheckedAt;
        device.UpdatedAt = outcome.CheckedAt;

        if (outcome.Result.Success)
        {
            device.LastSeenAt = outcome.CheckedAt;
            device.LastLatencyMs = outcome.Result.RoundtripTimeMs;
        }
        else
        {
            device.LastLatencyMs = null;
            logger.LogDebug(
                "Ping to device {IpAddress} failed: {Error}",
                device.IpAddress,
                outcome.Result.Error);
        }

        if (previousStatus != device.Status)
        {
            logger.LogInformation(
                "Device {IpAddress} changed {PreviousStatus} -> {CurrentStatus}",
                device.IpAddress,
                previousStatus,
                device.Status);
        }
    }

    private sealed record MonitoringTarget(int DeviceId, string IpAddress);

    private sealed record PingOutcome(
        MonitoringTarget Target,
        PingCheckResult Result,
        DateTimeOffset CheckedAt);
}
