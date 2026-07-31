using NetworkMonitor.Api.Dtos;

namespace NetworkMonitor.Api.Services;

public interface IEmailNotificationSettingsService
{
    Task<EmailNotificationSettingsResponse> GetAsync(CancellationToken cancellationToken);
    Task<EmailNotificationSettingsResponse> UpdateAsync(UpdateEmailNotificationSettingsRequest request, CancellationToken cancellationToken);
    Task SendTestAsync(CancellationToken cancellationToken);
}

public sealed class EmailNotificationValidationException(string message) : ArgumentException(message);
public sealed class EmailNotificationOperationException(string message) : InvalidOperationException(message);
