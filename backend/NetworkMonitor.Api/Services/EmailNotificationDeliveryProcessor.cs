using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetworkMonitor.Api.Configuration;
using NetworkMonitor.Api.Data;
using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Services;

public sealed class EmailNotificationDeliveryProcessor(
    NetworkMonitorDbContext dbContext,
    ISecretProtector secretProtector,
    IEmailSenderTransport emailSender,
    IOptions<EmailNotificationDeliveryOptions> options,
    ILogger<EmailNotificationDeliveryProcessor> logger) : IEmailNotificationDeliveryProcessor
{
    private readonly EmailNotificationDeliveryOptions _options = options.Value;
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5)];

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var attempts = await dbContext.NotificationDeliveryAttempts
            .Include(item => item.Notification).ThenInclude(item => item.Incident).ThenInclude(item => item!.Device)
            .Include(item => item.Notification).ThenInclude(item => item.Incident).ThenInclude(item => item!.SnmpMonitoredInterface)
            .Where(item => item.Channel == NotificationDeliveryChannel.Email
                && item.Status == NotificationDeliveryStatus.Pending
                && (item.NextAttemptAt == null || item.NextAttemptAt <= now))
            .OrderBy(item => item.NextAttemptAt).ThenBy(item => item.Id)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);
        if (attempts.Count == 0) return 0;

        var settings = await dbContext.EmailNotificationSettings.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        foreach (var attempt in attempts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProcessAttemptAsync(attempt, settings, cancellationToken);
        }
        return attempts.Count;
    }

    private async Task ProcessAttemptAsync(
        NotificationDeliveryAttempt attempt,
        EmailNotificationSettings? settings,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        attempt.AttemptCount++;
        attempt.LastAttemptAt = now;
        attempt.UpdatedAt = now;
        attempt.NextAttemptAt = null;

        if (settings is null || !settings.IsEnabled)
        {
            Fail(attempt, EmailDeliveryErrorCode.ChannelDisabled, true, now);
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        string? password;
        try
        {
            password = settings.ProtectedPassword is null ? null : secretProtector.Unprotect(settings.ProtectedPassword);
        }
        catch (Exception)
        {
            Fail(attempt, EmailDeliveryErrorCode.DecryptionFailed, true, now);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogWarning("Email delivery {DeliveryAttemptId} for notification {NotificationId} failed with {ErrorCode}.", attempt.Id, attempt.NotificationId, EmailDeliveryErrorCode.DecryptionFailed);
            return;
        }

        try
        {
            var recipients = settings.RecipientAddresses.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            await emailSender.SendAsync(BuildRequest(attempt.Notification, settings, recipients, password), cancellationToken);
            attempt.Status = NotificationDeliveryStatus.Sent;
            attempt.SentAt = now;
            attempt.LastErrorCode = null;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (EmailTransportException exception)
        {
            Fail(attempt, exception.ErrorCode, exception.IsPermanent, now);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogWarning("Email delivery {DeliveryAttemptId} for notification {NotificationId} via {SmtpHost} failed with {ErrorCode}.", attempt.Id, attempt.NotificationId, settings.Host, exception.ErrorCode);
        }
        catch (Exception)
        {
            Fail(attempt, EmailDeliveryErrorCode.Unexpected, false, now);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogWarning("Email delivery {DeliveryAttemptId} for notification {NotificationId} via {SmtpHost} failed with {ErrorCode}.", attempt.Id, attempt.NotificationId, settings.Host, EmailDeliveryErrorCode.Unexpected);
        }
    }

    private void Fail(NotificationDeliveryAttempt attempt, EmailDeliveryErrorCode errorCode, bool permanent, DateTimeOffset now)
    {
        attempt.LastErrorCode = errorCode;
        if (permanent || attempt.AttemptCount >= _options.MaxAttempts)
        {
            attempt.Status = NotificationDeliveryStatus.Failed;
            attempt.NextAttemptAt = null;
            return;
        }
        attempt.Status = NotificationDeliveryStatus.Pending;
        attempt.NextAttemptAt = now.Add(RetryDelays[Math.Min(attempt.AttemptCount - 1, RetryDelays.Length - 1)]);
    }

    private static EmailSendRequest BuildRequest(
        Notification notification,
        EmailNotificationSettings settings,
        IReadOnlyList<string> recipients,
        string? password)
    {
        var incident = notification.Incident;
        var deviceName = incident?.Device?.Name ?? "Unknown device";
        var interfaceName = incident?.SnmpMonitoredInterface?.InterfaceName;
        var context = string.IsNullOrWhiteSpace(interfaceName) ? deviceName : $"{deviceName} / {interfaceName}";
        var subject = $"[NetScope] {notification.Title} - {context}";
        var body = string.Join(Environment.NewLine,
            notification.Title,
            notification.Message,
            string.Empty,
            $"Device: {deviceName}",
            $"Incident type: {incident?.Type.ToString() ?? "Unavailable"}",
            $"Incident started: {incident?.StartedAt.ToUniversalTime():u}",
            string.IsNullOrWhiteSpace(interfaceName) ? null : $"Interface: {interfaceName}")
            .Replace($"{Environment.NewLine}{Environment.NewLine}{Environment.NewLine}", $"{Environment.NewLine}{Environment.NewLine}");
        return new EmailSendRequest(
            settings.Host, settings.Port, settings.TlsMode, settings.Username, password,
            settings.FromAddress, settings.FromName, recipients, subject, body);
    }
}
