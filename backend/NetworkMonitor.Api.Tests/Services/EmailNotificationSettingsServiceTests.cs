using Microsoft.AspNetCore.Mvc;
using NetworkMonitor.Api.Controllers;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Models;
using NetworkMonitor.Api.Services;
using NetworkMonitor.Api.Tests.Infrastructure;

namespace NetworkMonitor.Api.Tests.Services;

public sealed class EmailNotificationSettingsServiceTests
{
    [Fact]
    public async Task UpdateAsync_CreatesEncryptedSettingsAndNeverReturnsPassword()
    {
        await using var database = await TestDatabase.CreateAsync();
        var protector = new TestProtector();
        var service = new EmailNotificationSettingsService(database.Context, protector, new FakeEmailSender());

        var response = await service.UpdateAsync(ValidRequest() with { Password = "smtp-secret" }, CancellationToken.None);

        var entity = Assert.Single(database.Context.EmailNotificationSettings);
        Assert.NotEqual("smtp-secret", entity.ProtectedPassword);
        Assert.DoesNotContain("smtp-secret", entity.ProtectedPassword);
        Assert.True(response.HasPassword);
        Assert.DoesNotContain(response.GetType().GetProperties(), property => property.Name.Contains("Password", StringComparison.Ordinal) && property.Name != nameof(response.HasPassword));
    }

    [Fact]
    public async Task UpdateAsync_BlankPasswordPreservesExistingProtectedPassword()
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = new EmailNotificationSettingsService(database.Context, new TestProtector(), new FakeEmailSender());
        await service.UpdateAsync(ValidRequest() with { Password = "first-secret" }, CancellationToken.None);
        var protectedPassword = database.Context.EmailNotificationSettings.Single().ProtectedPassword;

        var response = await service.UpdateAsync(ValidRequest() with { Password = "", FromName = "Updated sender" }, CancellationToken.None);

        Assert.Equal(protectedPassword, database.Context.EmailNotificationSettings.Single().ProtectedPassword);
        Assert.True(response.HasPassword);
        Assert.Equal("Updated sender", response.FromName);
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task UpdateAsync_RejectsInvalidConfiguration(UpdateEmailNotificationSettingsRequest request)
    {
        await using var database = await TestDatabase.CreateAsync();
        var service = new EmailNotificationSettingsService(database.Context, new TestProtector(), new FakeEmailSender());
        await Assert.ThrowsAsync<EmailNotificationValidationException>(() => service.UpdateAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task SendTestAsync_UsesSavedSecretWithoutCreatingNotification()
    {
        await using var database = await TestDatabase.CreateAsync();
        var sender = new FakeEmailSender();
        var service = new EmailNotificationSettingsService(database.Context, new TestProtector(), sender);
        await service.UpdateAsync(ValidRequest() with { IsEnabled = false, Password = "smtp-secret" }, CancellationToken.None);

        await service.SendTestAsync(CancellationToken.None);

        var request = Assert.Single(sender.Requests);
        Assert.Equal("smtp-secret", request.Password);
        Assert.Equal("[NetScope] Test email", request.Subject);
        Assert.Empty(database.Context.Notifications);
        Assert.Empty(database.Context.NotificationDeliveryAttempts);
    }

    [Fact]
    public async Task Controller_ReturnsSafeSuccessAndValidationResponses()
    {
        await using var database = await TestDatabase.CreateAsync();
        var sender = new FakeEmailSender();
        var service = new EmailNotificationSettingsService(database.Context, new TestProtector(), sender);
        var controller = new EmailNotificationSettingsController(service);

        Assert.IsType<BadRequestObjectResult>((await controller.Update(ValidRequest() with { Host = "bad host" }, CancellationToken.None)).Result);
        await service.UpdateAsync(ValidRequest() with { Password = "smtp-secret" }, CancellationToken.None);
        var action = await controller.SendTest(CancellationToken.None);
        var response = Assert.IsType<TestEmailResponse>(Assert.IsType<OkObjectResult>(action.Result).Value);
        Assert.Equal("Test email sent.", response.Message);
    }

    public static IEnumerable<object[]> InvalidRequests()
    {
        yield return [ValidRequest() with { Host = "bad host" }];
        yield return [ValidRequest() with { Port = 0 }];
        yield return [ValidRequest() with { FromAddress = "not-an-email" }];
        yield return [ValidRequest() with { RecipientAddresses = ["bad-address"] }];
        yield return [ValidRequest() with { Host = "" }];
        yield return [ValidRequest() with { RecipientAddresses = [] }];
        yield return [ValidRequest() with { Password = null }];
        yield return [ValidRequest() with { Username = null, Password = "smtp-secret" }];
    }

    private static UpdateEmailNotificationSettingsRequest ValidRequest() => new()
    {
        IsEnabled = true,
        Host = "smtp.example.com",
        Port = 587,
        TlsMode = EmailTlsMode.StartTls,
        Username = "alerts",
        Password = "smtp-secret",
        FromAddress = "netscope@example.com",
        FromName = "NetScope",
        RecipientAddresses = ["alerts@example.com", "admin@example.com"]
    };

    private sealed class TestProtector : ISecretProtector
    {
        public string Protect(string secret) => $"protected:{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(secret))}";
        public string Unprotect(string protectedSecret) => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(protectedSecret["protected:".Length..]));
    }

    private sealed class FakeEmailSender : IEmailSenderTransport
    {
        public List<EmailSendRequest> Requests { get; } = [];
        public Task SendAsync(EmailSendRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.CompletedTask;
        }
    }
}
