using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Hubs;
using NetworkMonitor.Api.Models;
using NetworkMonitor.Api.Services;

namespace NetworkMonitor.Api.Tests.Services;

public sealed class SignalRMonitoringUpdatePublisherTests
{
    [Fact]
    public async Task PublishAsync_SendsExpectedEventAndDto()
    {
        var client = new RecordingClientProxy();
        var publisher = CreatePublisher(client);
        var update = new DeviceMonitoringUpdate(
            42,
            DeviceStatus.Up,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            7,
            true);

        await publisher.PublishAsync(update, CancellationToken.None);

        var message = Assert.Single(client.Messages);
        Assert.Equal(MonitoringHub.DeviceMonitoringUpdatedEvent, message.Method);
        Assert.Same(update, Assert.Single(message.Arguments));
    }

    [Fact]
    public async Task PublishAsync_WhenSignalRFails_DoesNotStopMonitoringFlow()
    {
        var client = new RecordingClientProxy { ThrowOnSend = true };
        var publisher = CreatePublisher(client);
        var update = new DeviceMonitoringUpdate(
            42,
            DeviceStatus.Warning,
            DateTimeOffset.UtcNow,
            null,
            null,
            true);

        var exception = await Record.ExceptionAsync(() =>
            publisher.PublishAsync(update, CancellationToken.None));

        Assert.Null(exception);
    }

    private static SignalRMonitoringUpdatePublisher CreatePublisher(IClientProxy client)
    {
        var hubContext = new TestHubContext(new TestHubClients(client));
        return new SignalRMonitoringUpdatePublisher(
            hubContext,
            NullLogger<SignalRMonitoringUpdatePublisher>.Instance);
    }

    private sealed class RecordingClientProxy : IClientProxy
    {
        public List<(string Method, IReadOnlyList<object?> Arguments)> Messages { get; } = [];

        public bool ThrowOnSend { get; init; }

        public Task SendCoreAsync(
            string method,
            object?[] args,
            CancellationToken cancellationToken = default)
        {
            if (ThrowOnSend)
            {
                throw new InvalidOperationException("Simulated SignalR failure.");
            }

            Messages.Add((method, args));
            return Task.CompletedTask;
        }
    }

    private sealed class TestHubContext(IHubClients clients) : IHubContext<MonitoringHub>
    {
        public IHubClients Clients { get; } = clients;

        public IGroupManager Groups { get; } = new TestGroupManager();
    }

    private sealed class TestHubClients(IClientProxy client) : IHubClients
    {
        public IClientProxy All => client;

        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => client;

        public IClientProxy Client(string connectionId) => client;

        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => client;

        public IClientProxy Group(string groupName) => client;

        public IClientProxy GroupExcept(
            string groupName,
            IReadOnlyList<string> excludedConnectionIds) => client;

        public IClientProxy Groups(IReadOnlyList<string> groupNames) => client;

        public IClientProxy User(string userId) => client;

        public IClientProxy Users(IReadOnlyList<string> userIds) => client;
    }

    private sealed class TestGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RemoveFromGroupAsync(
            string connectionId,
            string groupName,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
