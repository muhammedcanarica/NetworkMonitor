using Microsoft.AspNetCore.SignalR;

namespace NetworkMonitor.Api.Hubs;

public sealed class MonitoringHub : Hub
{
    public const string DeviceMonitoringUpdatedEvent = "DeviceMonitoringUpdated";
}
