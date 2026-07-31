using NetworkMonitor.Api.Dtos;

namespace NetworkMonitor.Api.Services;

public interface INotificationService
{
    Task<NotificationResponse?> CreateForIncidentAsync(long incidentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<NotificationResponse>> ListAsync(bool unreadOnly, int limit, CancellationToken cancellationToken);
    Task<int> GetUnreadCountAsync(CancellationToken cancellationToken);
    Task<bool> MarkAsReadAsync(long id, CancellationToken cancellationToken);
    Task<int> MarkAllAsReadAsync(CancellationToken cancellationToken);
}
