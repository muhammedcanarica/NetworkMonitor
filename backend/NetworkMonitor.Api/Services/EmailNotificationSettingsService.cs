using Microsoft.EntityFrameworkCore;
using MimeKit;
using NetworkMonitor.Api.Data;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Services;

public sealed class EmailNotificationSettingsService(
    NetworkMonitorDbContext dbContext,
    ISecretProtector secretProtector,
    IEmailSenderTransport emailSender) : IEmailNotificationSettingsService
{
    public async Task<EmailNotificationSettingsResponse> GetAsync(CancellationToken cancellationToken)
    {
        var entity = await dbContext.EmailNotificationSettings.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        return entity is null ? EmptyResponse() : ToResponse(entity);
    }

    public async Task<EmailNotificationSettingsResponse> UpdateAsync(UpdateEmailNotificationSettingsRequest request, CancellationToken cancellationToken)
    {
        var entity = await dbContext.EmailNotificationSettings.SingleOrDefaultAsync(cancellationToken);
        var normalized = Normalize(request, entity?.ProtectedPassword is not null);
        Validate(normalized, request.IsEnabled);
        var now = DateTimeOffset.UtcNow;
        if (entity is null)
        {
            entity = new EmailNotificationSettings { Id = 1, CreatedAt = now };
            dbContext.EmailNotificationSettings.Add(entity);
        }
        entity.IsEnabled = normalized.IsEnabled;
        entity.Host = normalized.Host;
        entity.Port = normalized.Port;
        entity.TlsMode = normalized.TlsMode;
        entity.Username = normalized.Username;
        entity.FromAddress = normalized.FromAddress;
        entity.FromName = normalized.FromName;
        entity.RecipientAddresses = string.Join(';', normalized.RecipientAddresses);
        entity.UpdatedAt = now;
        if (!string.IsNullOrWhiteSpace(request.Password)) entity.ProtectedPassword = secretProtector.Protect(request.Password);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(entity);
    }

    public async Task SendTestAsync(CancellationToken cancellationToken)
    {
        var entity = await dbContext.EmailNotificationSettings.AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw new EmailNotificationValidationException("Email notification settings are not configured.");
        var settings = FromEntity(entity);
        Validate(settings, true);
        string? password = null;
        try
        {
            if (entity.ProtectedPassword is not null) password = secretProtector.Unprotect(entity.ProtectedPassword);
        }
        catch (Exception exception)
        {
            throw new EmailNotificationOperationException($"The configured SMTP password could not be decrypted ({exception.GetType().Name}).");
        }
        try
        {
            await emailSender.SendAsync(new EmailSendRequest(
                settings.Host, settings.Port, settings.TlsMode, settings.Username, password,
                settings.FromAddress, settings.FromName, settings.RecipientAddresses,
                "[NetScope] Test email", "This is a test email from NetScope. SMTP delivery was accepted by the configured server."), cancellationToken);
        }
        catch (EmailTransportException exception)
        {
            throw new EmailNotificationOperationException($"Test email could not be sent: {exception.ErrorCode}.");
        }
    }

    internal static EmailSettingsValue FromEntity(EmailNotificationSettings entity) => new(
        entity.IsEnabled, entity.Host, entity.Port, entity.TlsMode, entity.Username,
        entity.FromAddress, entity.FromName, SplitRecipients(entity.RecipientAddresses),
        entity.ProtectedPassword is not null);

    internal static void Validate(EmailSettingsValue settings, bool requireComplete)
    {
        if (!Enum.IsDefined(settings.TlsMode)) throw new EmailNotificationValidationException("TLS mode is invalid.");
        if (settings.Port is < 1 or > 65535) throw new EmailNotificationValidationException("SMTP port must be between 1 and 65535.");
        if (!string.IsNullOrWhiteSpace(settings.Host) && Uri.CheckHostName(settings.Host) == UriHostNameType.Unknown)
            throw new EmailNotificationValidationException("SMTP host is invalid.");
        if (!string.IsNullOrWhiteSpace(settings.FromAddress) && !IsValidInternetAddress(settings.FromAddress))
            throw new EmailNotificationValidationException("From address is invalid.");
        if (settings.RecipientAddresses.Any(address => !IsValidInternetAddress(address)))
            throw new EmailNotificationValidationException("One or more recipient addresses are invalid.");
        if (settings.Username is null && settings.HasPassword)
            throw new EmailNotificationValidationException("SMTP username is required when a password is configured.");
        if (settings.Username is not null && !settings.HasPassword)
            throw new EmailNotificationValidationException("SMTP password is required when a username is configured.");
        if (requireComplete && string.IsNullOrWhiteSpace(settings.Host)) throw new EmailNotificationValidationException("SMTP host is required.");
        if (requireComplete && string.IsNullOrWhiteSpace(settings.FromAddress)) throw new EmailNotificationValidationException("From address is required.");
        if (requireComplete && settings.RecipientAddresses.Count == 0) throw new EmailNotificationValidationException("At least one recipient is required.");
    }

    private static bool IsValidInternetAddress(string value)
    {
        if (!MailboxAddress.TryParse(value, out var mailbox) || !string.Equals(mailbox.Address, value, StringComparison.OrdinalIgnoreCase)) return false;
        var separator = value.LastIndexOf('@');
        return separator > 0 && separator < value.Length - 1 && Uri.CheckHostName(value[(separator + 1)..]) != UriHostNameType.Unknown;
    }

    private static EmailSettingsValue Normalize(UpdateEmailNotificationSettingsRequest request, bool hasExistingPassword)
    {
        var recipients = request.RecipientAddresses.Select(item => item.Trim()).Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return new EmailSettingsValue(
            request.IsEnabled,
            request.Host.Trim(),
            request.Port,
            request.TlsMode,
            string.IsNullOrWhiteSpace(request.Username) ? null : request.Username.Trim(),
            request.FromAddress.Trim(),
            string.IsNullOrWhiteSpace(request.FromName) ? null : request.FromName.Trim(),
            recipients,
            hasExistingPassword || !string.IsNullOrWhiteSpace(request.Password));
    }

    private static IReadOnlyList<string> SplitRecipients(string value) => value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static EmailNotificationSettingsResponse ToResponse(EmailNotificationSettings entity) => new(
        entity.IsEnabled, entity.Host, entity.Port, entity.TlsMode, entity.Username,
        entity.FromAddress, entity.FromName, SplitRecipients(entity.RecipientAddresses),
        entity.ProtectedPassword is not null, entity.UpdatedAt);
    private static EmailNotificationSettingsResponse EmptyResponse() => new(false, string.Empty, 587, EmailTlsMode.StartTls, null, string.Empty, null, [], false, null);

    internal sealed record EmailSettingsValue(
        bool IsEnabled,
        string Host,
        int Port,
        EmailTlsMode TlsMode,
        string? Username,
        string FromAddress,
        string? FromName,
        IReadOnlyList<string> RecipientAddresses,
        bool HasPassword);
}
