using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Dtos;

public sealed record NotificationResponse(
    long Id,
    NotificationType Type,
    string Title,
    string Message,
    long? IncidentId,
    int? DeviceId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt,
    bool IsRead);

public sealed record NotificationUnreadCountResponse(int Count);
