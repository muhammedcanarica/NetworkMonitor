using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using NetworkMonitor.Api.Configuration;
using NetworkMonitor.Api.Data;
using NetworkMonitor.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

builder.Services.AddDbContext<NetworkMonitorDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services
    .AddOptions<MonitoringOptions>()
    .Bind(builder.Configuration.GetSection(MonitoringOptions.SectionName))
    .Validate(options => options.IntervalSeconds > 0, "Monitoring interval must be greater than zero.")
    .Validate(options => options.PingTimeoutMilliseconds > 0, "Ping timeout must be greater than zero.")
    .Validate(options => options.FailureThreshold > 0, "Failure threshold must be greater than zero.")
    .Validate(options => options.RecoveryThreshold > 0, "Recovery threshold must be greater than zero.")
    .Validate(options => options.MaxConcurrentPings > 0, "Maximum concurrent pings must be greater than zero.")
    .ValidateOnStart();
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddSingleton<IPingService, PingService>();
builder.Services.AddSingleton<DeviceStatusTracker>();
builder.Services.AddHostedService<DeviceMonitoringService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
