using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NetworkMonitor.Api.Configuration;
using NetworkMonitor.Api.Models;
using NetworkMonitor.Api.Services;
using NetworkMonitor.Api.Tests.Infrastructure;

namespace NetworkMonitor.Api.Tests.Services;

public sealed class EmailNotificationDeliveryTests
{
    [Fact]
    public async Task Planner_CreatesOnlyOneAttemptWhenEmailIsEnabled()
    {
        await using var database = await TestDatabase.CreateAsync();
        var notification = await AddNotification(database, IncidentType.DeviceUnreachable);
        var settings = AddSettings(database, enabled: false);
        await database.Context.SaveChangesAsync();
        var planner = new NotificationDeliveryPlanner(database.Context);

        await planner.ScheduleAsync(notification.Id, CancellationToken.None);
        Assert.Empty(database.Context.NotificationDeliveryAttempts);

        settings.IsEnabled = true;
        await database.Context.SaveChangesAsync();
        await planner.ScheduleAsync(notification.Id, CancellationToken.None);
        await planner.ScheduleAsync(notification.Id, CancellationToken.None);

        var attempt = Assert.Single(database.Context.NotificationDeliveryAttempts);
        Assert.Equal(NotificationDeliveryStatus.Pending, attempt.Status);
        Assert.Equal(NotificationDeliveryChannel.Email, attempt.Channel);
    }

    [Theory]
    [InlineData(IncidentType.DeviceUnreachable)]
    [InlineData(IncidentType.InterfaceDown)]
    [InlineData(IncidentType.InterfaceInboundBandwidthHigh)]
    [InlineData(IncidentType.InterfaceOutboundBandwidthHigh)]
    public async Task Planner_SupportsNotificationsFromAllIncidentTypes(IncidentType type)
    {
        await using var database = await TestDatabase.CreateAsync();
        var notification = await AddNotification(database, type);
        AddSettings(database, enabled: true);
        await database.Context.SaveChangesAsync();

        await new NotificationDeliveryPlanner(database.Context).ScheduleAsync(notification.Id, CancellationToken.None);

        Assert.Single(database.Context.NotificationDeliveryAttempts);
    }

    [Fact]
    public async Task DatabaseConstraint_RejectsDuplicateEmailDelivery()
    {
        await using var database = await TestDatabase.CreateAsync();
        var notification = await AddNotification(database, IncidentType.DeviceUnreachable);
        var now = DateTimeOffset.UtcNow;
        database.Context.NotificationDeliveryAttempts.AddRange(Attempt(notification.Id, now), Attempt(notification.Id, now));
        await Assert.ThrowsAsync<DbUpdateException>(() => database.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task Processor_SendsPendingAttemptAndPersistsSentStateAndContent()
    {
        await using var database = await TestDatabase.CreateAsync();
        var notification = await AddNotification(database, IncidentType.InterfaceDown);
        AddSettings(database, enabled: true);
        database.Context.NotificationDeliveryAttempts.Add(Attempt(notification.Id, DateTimeOffset.UtcNow));
        await database.Context.SaveChangesAsync();
        var sender = new FakeEmailSender();

        var processed = await Processor(database, sender).ProcessBatchAsync(CancellationToken.None);

        var attempt = Assert.Single(database.Context.NotificationDeliveryAttempts);
        Assert.Equal(1, processed);
        Assert.Equal(NotificationDeliveryStatus.Sent, attempt.Status);
        Assert.NotNull(attempt.SentAt);
        Assert.Equal(1, attempt.AttemptCount);
        var request = Assert.Single(sender.Requests);
        Assert.Contains("[NetScope] Interface down - Test Device", request.Subject);
        Assert.Contains("Incident type: InterfaceDown", request.PlainTextBody);
        Assert.Equal("smtp-secret", request.Password);
    }

    [Fact]
    public async Task Processor_RetriesTransientFailureThenStopsAtMaxAttemptsWithoutDeletingRecords()
    {
        await using var database = await TestDatabase.CreateAsync();
        var notification = await AddNotification(database, IncidentType.DeviceUnreachable);
        AddSettings(database, enabled: true);
        database.Context.NotificationDeliveryAttempts.Add(Attempt(notification.Id, DateTimeOffset.UtcNow));
        await database.Context.SaveChangesAsync();
        var sender = new FakeEmailSender(_ => new EmailTransportException(EmailDeliveryErrorCode.ConnectionFailed, false, "safe"));
        var processor = Processor(database, sender, maxAttempts: 3);

        await processor.ProcessBatchAsync(CancellationToken.None);
        var attempt = database.Context.NotificationDeliveryAttempts.Single();
        Assert.Equal(NotificationDeliveryStatus.Pending, attempt.Status);
        Assert.InRange(attempt.NextAttemptAt!.Value - attempt.LastAttemptAt!.Value, TimeSpan.FromSeconds(59), TimeSpan.FromSeconds(61));

        attempt.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        await database.Context.SaveChangesAsync();
        await processor.ProcessBatchAsync(CancellationToken.None);
        Assert.Equal(NotificationDeliveryStatus.Pending, attempt.Status);
        Assert.InRange(attempt.NextAttemptAt!.Value - attempt.LastAttemptAt!.Value, TimeSpan.FromMinutes(4.9), TimeSpan.FromMinutes(5.1));

        attempt.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        await database.Context.SaveChangesAsync();
        await processor.ProcessBatchAsync(CancellationToken.None);
        Assert.Equal(NotificationDeliveryStatus.Failed, attempt.Status);
        Assert.Equal(3, attempt.AttemptCount);
        Assert.Equal(EmailDeliveryErrorCode.ConnectionFailed, attempt.LastErrorCode);
        Assert.NotNull(await database.Context.Notifications.FindAsync(notification.Id));
        Assert.NotNull(await database.Context.Incidents.FindAsync(notification.IncidentId));
    }

    [Fact]
    public async Task Processor_HandlesDecryptionFailureWithoutCallingTransportOrCrashing()
    {
        await using var database = await TestDatabase.CreateAsync();
        var notification = await AddNotification(database, IncidentType.DeviceUnreachable);
        AddSettings(database, enabled: true);
        database.Context.NotificationDeliveryAttempts.Add(Attempt(notification.Id, DateTimeOffset.UtcNow));
        await database.Context.SaveChangesAsync();
        var sender = new FakeEmailSender();
        var processor = new EmailNotificationDeliveryProcessor(
            database.Context, new ThrowingProtector(), sender, Options.Create(OptionsValue()), NullLogger<EmailNotificationDeliveryProcessor>.Instance);

        await processor.ProcessBatchAsync(CancellationToken.None);

        var attempt = database.Context.NotificationDeliveryAttempts.Single();
        Assert.Equal(NotificationDeliveryStatus.Failed, attempt.Status);
        Assert.Equal(EmailDeliveryErrorCode.DecryptionFailed, attempt.LastErrorCode);
        Assert.Empty(sender.Requests);
    }

    [Fact]
    public async Task Processor_RespectsBatchLimitCancellationAndRestartPersistence()
    {
        await using var database = await TestDatabase.CreateAsync();
        AddSettings(database, enabled: true);
        for (var index = 0; index < 3; index++)
        {
            var notification = await AddNotification(database, IncidentType.DeviceUnreachable);
            database.Context.NotificationDeliveryAttempts.Add(Attempt(notification.Id, DateTimeOffset.UtcNow));
        }
        await database.Context.SaveChangesAsync();
        var sender = new FakeEmailSender();

        Assert.Equal(2, await Processor(database, sender, batchSize: 2).ProcessBatchAsync(CancellationToken.None));
        Assert.Equal(2, database.Context.NotificationDeliveryAttempts.Count(item => item.Status == NotificationDeliveryStatus.Sent));
        Assert.Single(database.Context.NotificationDeliveryAttempts.Where(item => item.Status == NotificationDeliveryStatus.Pending));

        database.Context.ChangeTracker.Clear();
        Assert.Equal(1, await Processor(database, sender, batchSize: 2).ProcessBatchAsync(CancellationToken.None));
        Assert.Equal(3, database.Context.NotificationDeliveryAttempts.Count(item => item.Status == NotificationDeliveryStatus.Sent));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Processor(database, sender).ProcessBatchAsync(cancellation.Token));
    }

    private static EmailNotificationDeliveryProcessor Processor(TestDatabase database, FakeEmailSender sender, int batchSize = 20, int maxAttempts = 3)
        => new(database.Context, new TestProtector(), sender, Options.Create(OptionsValue(batchSize, maxAttempts)), NullLogger<EmailNotificationDeliveryProcessor>.Instance);

    private static EmailNotificationDeliveryOptions OptionsValue(int batchSize = 20, int maxAttempts = 3)
        => new() { PollIntervalSeconds = 10, BatchSize = batchSize, MaxAttempts = maxAttempts };

    private static EmailNotificationSettings AddSettings(TestDatabase database, bool enabled)
    {
        var now = DateTimeOffset.UtcNow;
        var entity = new EmailNotificationSettings
        {
            Id = 1, IsEnabled = enabled, Host = "smtp.example.com", Port = 587, TlsMode = EmailTlsMode.StartTls,
            Username = "alerts", ProtectedPassword = new TestProtector().Protect("smtp-secret"),
            FromAddress = "netscope@example.com", FromName = "NetScope", RecipientAddresses = "alerts@example.com;admin@example.com",
            CreatedAt = now, UpdatedAt = now
        };
        database.Context.EmailNotificationSettings.Add(entity);
        return entity;
    }

    private static async Task<Notification> AddNotification(TestDatabase database, IncidentType type)
    {
        var device = await database.AddDeviceAsync();
        var now = DateTimeOffset.UtcNow;
        var incident = new Incident
        {
            DeviceId = device.Id, Type = type, Status = IncidentStatus.Open, Summary = "Test incident",
            StartedAt = now, CreatedAt = now, UpdatedAt = now
        };
        var notification = new Notification
        {
            Type = NotificationType.IncidentOpened, Title = type == IncidentType.InterfaceDown ? "Interface down" : "Device unreachable",
            Message = "Monitoring incident opened.", DeviceId = device.Id, Incident = incident, CreatedAt = now
        };
        database.Context.Add(notification);
        await database.Context.SaveChangesAsync();
        return notification;
    }

    private static NotificationDeliveryAttempt Attempt(long notificationId, DateTimeOffset now) => new()
    {
        NotificationId = notificationId, Channel = NotificationDeliveryChannel.Email,
        Status = NotificationDeliveryStatus.Pending, NextAttemptAt = now,
        CreatedAt = now, UpdatedAt = now
    };

    private sealed class TestProtector : ISecretProtector
    {
        public string Protect(string secret) => $"protected:{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(secret))}";
        public string Unprotect(string protectedSecret) => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(protectedSecret["protected:".Length..]));
    }

    private sealed class ThrowingProtector : ISecretProtector
    {
        public string Protect(string secret) => throw new NotSupportedException();
        public string Unprotect(string protectedSecret) => throw new InvalidOperationException("key ring unavailable");
    }

    private sealed class FakeEmailSender(Func<int, Exception?>? failure = null) : IEmailSenderTransport
    {
        public List<EmailSendRequest> Requests { get; } = [];
        public Task SendAsync(EmailSendRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            var exception = failure?.Invoke(Requests.Count);
            return exception is null ? Task.CompletedTask : Task.FromException(exception);
        }
    }
}
