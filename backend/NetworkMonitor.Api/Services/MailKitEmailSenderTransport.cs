using System.Net.Sockets;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Services;

public sealed class MailKitEmailSenderTransport : IEmailSenderTransport
{
    public async Task SendAsync(EmailSendRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(request.FromName ?? string.Empty, request.FromAddress));
            foreach (var recipient in request.RecipientAddresses)
            {
                message.To.Add(MailboxAddress.Parse(recipient));
            }
            message.Subject = request.Subject;
            message.Body = new TextPart("plain") { Text = request.PlainTextBody };

            using var client = new SmtpClient();
            await client.ConnectAsync(request.Host, request.Port, ToSocketOptions(request.TlsMode), cancellationToken);
            if (!string.IsNullOrWhiteSpace(request.Username))
            {
                await client.AuthenticateAsync(request.Username, request.Password ?? string.Empty, cancellationToken);
            }
            await client.SendAsync(message, cancellationToken);
            try { await client.DisconnectAsync(true, CancellationToken.None); } catch { /* Delivery was already accepted. */ }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (MailKit.Security.AuthenticationException exception)
        {
            throw new EmailTransportException(EmailDeliveryErrorCode.AuthenticationFailed, true, "SMTP authentication failed.", exception);
        }
        catch (SmtpCommandException exception) when (exception.ErrorCode == SmtpErrorCode.RecipientNotAccepted)
        {
            throw new EmailTransportException(EmailDeliveryErrorCode.RecipientRejected, true, "The SMTP server rejected a recipient.", exception);
        }
        catch (SmtpCommandException exception) when (exception.ErrorCode == SmtpErrorCode.SenderNotAccepted)
        {
            throw new EmailTransportException(EmailDeliveryErrorCode.InvalidConfiguration, true, "The SMTP server rejected the sender.", exception);
        }
        catch (TimeoutException exception)
        {
            throw new EmailTransportException(EmailDeliveryErrorCode.Timeout, false, "The SMTP operation timed out.", exception);
        }
        catch (Exception exception) when (exception is SocketException or IOException or SmtpProtocolException or SmtpCommandException)
        {
            throw new EmailTransportException(EmailDeliveryErrorCode.ConnectionFailed, false, "The SMTP connection failed.", exception);
        }
        catch (FormatException exception)
        {
            throw new EmailTransportException(EmailDeliveryErrorCode.InvalidConfiguration, true, "An email address is invalid.", exception);
        }
    }

    private static SecureSocketOptions ToSocketOptions(EmailTlsMode mode) => mode switch
    {
        EmailTlsMode.None => SecureSocketOptions.None,
        EmailTlsMode.StartTls => SecureSocketOptions.StartTls,
        EmailTlsMode.SslOnConnect => SecureSocketOptions.SslOnConnect,
        _ => throw new EmailTransportException(EmailDeliveryErrorCode.InvalidConfiguration, true, "The TLS mode is invalid.")
    };
}
