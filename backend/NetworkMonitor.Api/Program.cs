using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using NetworkMonitor.Api.Configuration;
using NetworkMonitor.Api.Data;
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
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
if (frontendOrigins.Length > 0)
{
    builder.Services.AddCors(options =>
        options.AddPolicy("Frontend", policy =>
            policy.WithOrigins(frontendOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()));
}
builder.Services.AddSingleton<IPingService, PingService>();
builder.Services.AddSingleton<DeviceStatusTracker>();
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

app.Run();
