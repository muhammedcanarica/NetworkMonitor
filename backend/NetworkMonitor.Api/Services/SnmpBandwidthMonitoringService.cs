using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetworkMonitor.Api.Configuration;
using NetworkMonitor.Api.Data;

namespace NetworkMonitor.Api.Services;

public sealed class SnmpBandwidthMonitoringService(
    IServiceScopeFactory scopeFactory,
    IOptions<SnmpBandwidthMonitoringOptions> options,
    ILogger<SnmpBandwidthMonitoringService> logger) : BackgroundService
{
    private readonly SnmpBandwidthMonitoringOptions _options = options.Value;
    private DateTimeOffset _nextCleanupAt = DateTimeOffset.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError("SNMP bandwidth monitoring cycle failed with {ErrorType}.", exception.GetType().Name);
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

    public async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        int[] profileIds;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<NetworkMonitorDbContext>();
            await CleanupHistoryIfDue(dbContext, cancellationToken);
            profileIds = await dbContext.SnmpMonitoringProfiles.AsNoTracking()
                .Where(item => item.IsEnabled && item.Interfaces.Any(value => value.IsEnabled))
                .Select(item => item.Id)
                .ToArrayAsync(cancellationToken);
        }

        await Parallel.ForEachAsync(profileIds, new ParallelOptions
        {
            MaxDegreeOfParallelism = _options.MaxConcurrentDevices,
            CancellationToken = cancellationToken
        }, async (profileId, token) =>
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ISnmpBandwidthProfilePoller>().PollAsync(profileId, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning("SNMP bandwidth poll for profile {ProfileId} was skipped ({ErrorType}).", profileId, exception.GetType().Name);
            }
        });
    }

    private async Task CleanupHistoryIfDue(NetworkMonitorDbContext dbContext, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        if (now < _nextCleanupAt) return;
        _nextCleanupAt = now.AddDays(1);
        var cutoff = now.AddDays(-_options.HistoryRetentionDays);
        var deleted = await dbContext.InterfaceTrafficSamples.Where(item => item.Timestamp < cutoff).ExecuteDeleteAsync(cancellationToken);
        if (deleted > 0) logger.LogInformation("Deleted {SampleCount} interface traffic samples older than {Cutoff}.", deleted, cutoff);
    }
}
