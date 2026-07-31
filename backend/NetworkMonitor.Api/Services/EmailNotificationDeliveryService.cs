using Microsoft.Extensions.Options;
using NetworkMonitor.Api.Configuration;

namespace NetworkMonitor.Api.Services;

public sealed class EmailNotificationDeliveryService(
    IServiceScopeFactory scopeFactory,
    IOptions<EmailNotificationDeliveryOptions> options,
    ILogger<EmailNotificationDeliveryService> logger) : BackgroundService
{
    private readonly EmailNotificationDeliveryOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IEmailNotificationDeliveryProcessor>().ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError("Email notification delivery cycle failed with {ErrorType}.", exception.GetType().Name);
            }

            try { await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
