namespace NetworkMonitor.Api.Services;

public interface IInterfaceStatusIncidentEvaluator
{
    Task EvaluateAsync(int monitoredInterfaceId, string? adminStatus, string? operStatus, DateTimeOffset timestamp, CancellationToken cancellationToken);
}
