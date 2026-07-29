using Microsoft.AspNetCore.SignalR;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Hubs;

namespace NetworkMonitor.Api.Services;

public sealed class SignalRMonitoringUpdatePublisher(
    IHubContext<MonitoringHub> hubContext,
    ILogger<SignalRMonitoringUpdatePublisher> logger) : IMonitoringUpdatePublisher
{
    public async Task PublishAsync(
        DeviceMonitoringUpdate update,
        CancellationToken cancellationToken)
    {
        try
        {
            await hubContext.Clients.All.SendAsync(
                MonitoringHub.DeviceMonitoringUpdatedEvent,
                update,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Could not publish monitoring update for device {DeviceId}.",
                update.DeviceId);
        }
    }
}
