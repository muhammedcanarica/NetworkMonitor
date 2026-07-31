using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NetworkMonitor.Api.Data;
using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Services;

public sealed class NotificationDeliveryPlanner(NetworkMonitorDbContext dbContext) : INotificationDeliveryPlanner
{
    public async Task ScheduleAsync(long notificationId, CancellationToken cancellationToken)
    {
        if (!await dbContext.EmailNotificationSettings.AsNoTracking().AnyAsync(item => item.IsEnabled, cancellationToken)) return;
        if (await dbContext.NotificationDeliveryAttempts.AsNoTracking().AnyAsync(
                item => item.NotificationId == notificationId && item.Channel == NotificationDeliveryChannel.Email,
                cancellationToken)) return;

        var now = DateTimeOffset.UtcNow;
        var attempt = new NotificationDeliveryAttempt
        {
            NotificationId = notificationId,
            Channel = NotificationDeliveryChannel.Email,
            Status = NotificationDeliveryStatus.Pending,
            NextAttemptAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.NotificationDeliveryAttempts.Add(attempt);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException { SqliteErrorCode: 19 })
        {
            dbContext.Entry(attempt).State = EntityState.Detached;
            if (!await dbContext.NotificationDeliveryAttempts.AsNoTracking().AnyAsync(
                    item => item.NotificationId == notificationId && item.Channel == NotificationDeliveryChannel.Email,
                    cancellationToken)) throw;
        }
    }
}
