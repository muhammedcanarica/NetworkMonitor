using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using NetworkMonitor.Api.Configuration;
using NetworkMonitor.Api.Data;
using NetworkMonitor.Api.Hubs;
using NetworkMonitor.Api.Models;
using NetworkMonitor.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");
var frontendOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];
var keyRingPath = Environment.GetEnvironmentVariable("NETSCOPE_KEY_RING_PATH")
    ?? Path.Combine(builder.Environment.ContentRootPath, ".keys");
Directory.CreateDirectory(keyRingPath);

builder.Services.AddDbContext<NetworkMonitorDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keyRingPath)).SetApplicationName("NetworkMonitor");
builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequiredLength = 10;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
    })
    .AddEntityFrameworkStores<NetworkMonitorDbContext>()
    .AddSignInManager();
builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "NetScope.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
    options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAuthorizationBuilder().SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddAntiforgery(options => { options.HeaderName = "X-CSRF-TOKEN"; options.Cookie.Name = "NetScope.Csrf"; options.Cookie.HttpOnly = true; options.Cookie.SameSite = SameSiteMode.Lax; options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; });
builder.Services.AddRateLimiter(options => options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));
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
    .AddOptions<PortScannerOptions>()
    .Bind(builder.Configuration.GetSection(PortScannerOptions.SectionName))
    .Validate(options => options.MaxPortsPerScan > 0, "Port scanner port limit must be greater than zero.")
    .Validate(options => options.MaxConcurrentConnections > 0, "Port scanner concurrency must be greater than zero.")
    .Validate(options => options.MinimumTimeoutMilliseconds > 0, "Port scanner minimum timeout must be greater than zero.")
    .Validate(
        options => options.MaximumTimeoutMilliseconds >= options.MinimumTimeoutMilliseconds,
        "Port scanner maximum timeout must be greater than or equal to the minimum timeout.")
    .ValidateOnStart();
builder.Services
    .AddOptions<ConfigBackupOptions>()
    .Bind(builder.Configuration.GetSection(ConfigBackupOptions.SectionName))
    .Validate(options => options.ConnectionTimeoutMilliseconds > 0, "Configuration backup connection timeout must be greater than zero.")
    .Validate(options => options.CommandTimeoutMilliseconds > 0, "Configuration backup command timeout must be greater than zero.")
    .Validate(options => options.MaxStoredConfigurationLength > 0, "Configuration backup storage limit must be greater than zero.")
    .Validate(options => options.MaxDiffLines > 0, "Configuration backup diff line limit must be greater than zero.")
    .ValidateOnStart();
builder.Services
    .AddOptions<TopologyDiscoveryOptions>()
    .Bind(builder.Configuration.GetSection(TopologyDiscoveryOptions.SectionName))
    .Validate(options => options.MaxDevicesPerDiscovery > 0, "Topology device limit must be greater than zero.")
    .Validate(options => options.MaxConcurrentDiscoveries > 0, "Topology concurrency must be greater than zero.")
    .ValidateOnStart();
builder.Services
    .AddOptions<SnmpBandwidthMonitoringOptions>()
    .Bind(builder.Configuration.GetSection(SnmpBandwidthMonitoringOptions.SectionName))
    .Validate(options => options.IntervalSeconds is >= SnmpBandwidthMonitoringOptions.MinimumIntervalSeconds and <= SnmpBandwidthMonitoringOptions.MaximumIntervalSeconds, "SNMP bandwidth interval must be between 15 and 3600 seconds.")
    .Validate(options => options.MaxConcurrentDevices > 0, "SNMP bandwidth concurrency must be greater than zero.")
    .Validate(options => options.HistoryRetentionDays > 0, "SNMP bandwidth retention must be greater than zero.")
    .Validate(options => options.RequestTimeoutMilliseconds is >= 500 and <= 10000, "SNMP bandwidth request timeout must be between 500 and 10000 milliseconds.")
    .Validate(options => options.InterfaceDownTriggerSamples is >= 1 and <= 20, "Interface down trigger samples must be between 1 and 20.")
    .Validate(options => options.InterfaceUpRecoverySamples is >= 1 and <= 20, "Interface up recovery samples must be between 1 and 20.")
    .ValidateOnStart();
builder.Services
    .AddOptions<EmailNotificationDeliveryOptions>()
    .Bind(builder.Configuration.GetSection(EmailNotificationDeliveryOptions.SectionName))
    .Validate(options => options.PollIntervalSeconds > 0, "Email delivery poll interval must be greater than zero.")
    .Validate(options => options.BatchSize is >= 1 and <= 100, "Email delivery batch size must be between 1 and 100.")
    .Validate(options => options.MaxAttempts is >= 1 and <= 10, "Email delivery max attempts must be between 1 and 10.")
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
builder.Services.AddSingleton<ITcpPortProbe, TcpPortProbe>();
builder.Services.AddSingleton<IPortScannerService, PortScannerService>();
builder.Services.AddSingleton<ISshCommandTransport, SshCommandTransport>();
builder.Services.AddSingleton<IConfigBackupProvider, CiscoIosConfigBackupProvider>();
builder.Services.AddSingleton<ConfigBackupProviderResolver>();
builder.Services.AddSingleton<IConfigBackupService, ConfigBackupService>();
builder.Services.AddSingleton<IConfigDiffService, ConfigDiffService>();
builder.Services.AddScoped<IConfigBackupStorageService, ConfigBackupStorageService>();
builder.Services.AddScoped<IIncidentService, IncidentService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IIncidentNotificationPublisher, IncidentNotificationPublisher>();
builder.Services.AddScoped<INotificationDeliveryPlanner, NotificationDeliveryPlanner>();
builder.Services.AddScoped<IEmailNotificationSettingsService, EmailNotificationSettingsService>();
builder.Services.AddSingleton<IEmailSenderTransport, MailKitEmailSenderTransport>();
builder.Services.AddScoped<IEmailNotificationDeliveryProcessor, EmailNotificationDeliveryProcessor>();
builder.Services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
builder.Services.AddScoped<INetworkCredentialService, NetworkCredentialService>();
builder.Services.AddScoped<INetworkOperationCredentialResolver, NetworkOperationCredentialResolver>();
builder.Services.AddSingleton<IWakeOnLanPacketSender, UdpWakeOnLanPacketSender>();
builder.Services.AddSingleton<IWakeOnLanService, WakeOnLanService>();
builder.Services.AddSingleton<ISnmpTransport, SharpSnmpTransport>();
builder.Services.AddSingleton<ISnmpService, SnmpService>();
builder.Services.AddSingleton<ISnmpBandwidthProbe, SnmpBandwidthProbe>();
builder.Services.AddScoped<ISnmpMonitoringConfigurationService, SnmpMonitoringConfigurationService>();
builder.Services.AddScoped<IInterfaceBandwidthThresholdService, InterfaceBandwidthThresholdService>();
builder.Services.AddScoped<IInterfaceBandwidthThresholdEvaluator, InterfaceBandwidthThresholdEvaluator>();
builder.Services.AddScoped<IInterfaceStatusIncidentEvaluator, InterfaceStatusIncidentEvaluator>();
builder.Services.AddScoped<ISnmpBandwidthProfilePoller, SnmpBandwidthProfilePoller>();
builder.Services.AddScoped<ITopologyDiscoveryService, TopologyDiscoveryService>();
builder.Services.AddSingleton<DeviceStatusTracker>();
builder.Services.AddSingleton<IMonitoringUpdatePublisher, SignalRMonitoringUpdatePublisher>();
builder.Services.AddHostedService<DeviceMonitoringService>();
builder.Services.AddHostedService<SnmpBandwidthMonitoringService>();
builder.Services.AddHostedService<EmailNotificationDeliveryService>();

var app = builder.Build();
await AdminBootstrapper.BootstrapAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
if (frontendOrigins.Length > 0)
{
    app.UseCors("Frontend");
}
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method) && !HttpMethods.IsOptions(context.Request.Method))
    {
        try
        {
            await context.RequestServices.GetRequiredService<Microsoft.AspNetCore.Antiforgery.IAntiforgery>().ValidateRequestAsync(context);
        }
        catch (Microsoft.AspNetCore.Antiforgery.AntiforgeryValidationException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }
    }
    await next(context);
});
app.MapControllers();
app.MapHub<MonitoringHub>("/hubs/monitoring");

app.Run();

public partial class Program;
