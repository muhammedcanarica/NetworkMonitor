using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NetworkMonitor.Api.Data;
using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Tests.Infrastructure;

internal sealed class TestDatabase : IAsyncDisposable
{
    private TestDatabase(SqliteConnection connection, NetworkMonitorDbContext context)
    {
        Connection = connection;
        Context = context;
    }

    public SqliteConnection Connection { get; }

    public NetworkMonitorDbContext Context { get; }

    public static async Task<TestDatabase> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<NetworkMonitorDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new NetworkMonitorDbContext(options);
        await context.Database.EnsureCreatedAsync();

        return new TestDatabase(connection, context);
    }

    public async Task<Device> AddDeviceAsync(
        string name = "Test Device",
        string? ipAddress = null)
    {
        var now = DateTimeOffset.UtcNow;
        var device = new Device
        {
            Name = name,
            IpAddress = ipAddress ?? $"192.0.2.{Context.Devices.Count() + 1}",
            Status = DeviceStatus.Unknown,
            IsMonitoringEnabled = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        Context.Devices.Add(device);
        await Context.SaveChangesAsync();
        return device;
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await Connection.DisposeAsync();
    }
}
