namespace NetworkMonitor.Api.Services;

public interface IEmailNotificationDeliveryProcessor
{
    Task<int> ProcessBatchAsync(CancellationToken cancellationToken);
}
