using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using NetworkMonitor.Api.Configuration;
using NetworkMonitor.Api.Data;
using NetworkMonitor.Api.Hubs;
using NetworkMonitor.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");
var frontendOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

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
    .Validate(options => options.HistoryRetentionDays > 0, "History retention days must be greater than zero.")
    .ValidateOnStart();
builder.Services
    .AddOptions<IpScannerOptions>()
    .Bind(builder.Configuration.GetSection(IpScannerOptions.SectionName))
    .Validate(options => options.PingTimeoutMilliseconds > 0, "IP scanner ping timeout must be greater than zero.")
    .Validate(options => options.MaxConcurrentPings > 0, "IP scanner concurrency must be greater than zero.")
    .Validate(options => options.MaxAddressesPerScan > 0, "IP scanner address limit must be greater than zero.")
    .Validate(options => options.HostNameTimeoutMilliseconds > 0, "IP scanner host name timeout must be greater than zero.")
    .ValidateOnStart();
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services
    .AddSignalR()
    .AddJsonProtocol(options =>
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
if (frontendOrigins.Length > 0)
{
    builder.Services.AddCors(options =>
        options.AddPolicy("Frontend", policy =>
            policy.WithOrigins(frontendOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()));
}
builder.Services.AddSingleton<IPingService, PingService>();
builder.Services.AddSingleton<IHostNameResolver, HostNameResolver>();
builder.Services.AddScoped<IIpScannerService, IpScannerService>();
builder.Services.AddSingleton<ISnmpTransport, SharpSnmpTransport>();
builder.Services.AddSingleton<ISnmpService, SnmpService>();
builder.Services.AddSingleton<DeviceStatusTracker>();
builder.Services.AddSingleton<IMonitoringUpdatePublisher, SignalRMonitoringUpdatePublisher>();
builder.Services.AddHostedService<DeviceMonitoringService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
if (frontendOrigins.Length > 0)
{
    app.UseCors("Frontend");
}
app.MapControllers();
app.MapHub<MonitoringHub>("/hubs/monitoring");

app.Run();
