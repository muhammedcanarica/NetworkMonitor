namespace NetworkMonitor.Api.Models;

public sealed class NotificationDeliveryAttempt
{
    public long Id { get; set; }
    public long NotificationId { get; set; }
    public NotificationDeliveryChannel Channel { get; set; }
    public NotificationDeliveryStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public EmailDeliveryErrorCode? LastErrorCode { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Notification Notification { get; set; } = null!;
}

public enum NotificationDeliveryChannel { Email }
public enum NotificationDeliveryStatus { Pending, Sent, Failed }
public enum EmailDeliveryErrorCode
{
    AuthenticationFailed,
    ConnectionFailed,
    RecipientRejected,
    Timeout,
    InvalidConfiguration,
    DecryptionFailed,
    ChannelDisabled,
    Unexpected
}
