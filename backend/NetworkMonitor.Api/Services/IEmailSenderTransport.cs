using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Services;

public interface IEmailSenderTransport
{
    Task SendAsync(EmailSendRequest request, CancellationToken cancellationToken);
}

public sealed record EmailSendRequest(
    string Host,
    int Port,
    EmailTlsMode TlsMode,
    string? Username,
    string? Password,
    string FromAddress,
    string? FromName,
    IReadOnlyList<string> RecipientAddresses,
    string Subject,
    string PlainTextBody);

public sealed class EmailTransportException(
    EmailDeliveryErrorCode errorCode,
    bool isPermanent,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    public EmailDeliveryErrorCode ErrorCode { get; } = errorCode;
    public bool IsPermanent { get; } = isPermanent;
}
