using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetworkMonitor.Api.Data;
using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Tests.Controllers;

public sealed class AuthenticationIntegrationTests
{
    [Fact]
    public async Task LoginCookie_ProtectsEndpointAndLogoutRevokesAccess()
    {
        await using var factory = new AuthFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await factory.InitializeAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/devices")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/devices/1/interface-traffic")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/devices/1/interfaces/1/bandwidth-threshold")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/notifications")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/notifications/unread-count")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/notification-settings/email")).StatusCode);
        var loginWithoutCsrf = await client.PostAsJsonAsync("/api/auth/login", new { username = "admin", password = "Correct-password1" });
        Assert.Equal(HttpStatusCode.BadRequest, loginWithoutCsrf.StatusCode);
        var csrf = await client.GetFromJsonAsync<CsrfResponse>("/api/security/csrf");

        var invalid = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login") { Content = JsonContent.Create(new { username = "admin", password = "wrong-password" }) };
        invalid.Headers.Add("X-CSRF-TOKEN", csrf!.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(invalid)).StatusCode);

        var valid = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login") { Content = JsonContent.Create(new { username = "admin", password = "Correct-password1" }) };
        valid.Headers.Add("X-CSRF-TOKEN", csrf.Token);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(valid)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/devices")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/notifications")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/notification-settings/email")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/notifications?limit=101")).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/devices")).StatusCode);

        csrf = await client.GetFromJsonAsync<CsrfResponse>("/api/security/csrf");
        var logout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout"); logout.Headers.Add("X-CSRF-TOKEN", csrf!.Token);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(logout)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/devices")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/devices/1/interface-traffic")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/devices/1/interfaces/1/bandwidth-threshold")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/notifications")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/notification-settings/email")).StatusCode);
    }

    [Fact]
    public async Task SignalRNegotiate_BypassesAntiforgeryButStillRequiresAuthentication()
    {
        await using var factory = new AuthFactory();
        var anonymousClient = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await factory.InitializeAsync();

        var anonymousNegotiate = await anonymousClient.PostAsync(
            "/hubs/monitoring/negotiate?negotiateVersion=1",
            new StringContent(string.Empty));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousNegotiate.StatusCode);

        var authenticatedClient = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var csrf = await authenticatedClient.GetFromJsonAsync<CsrfResponse>("/api/security/csrf");
        var login = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { username = "admin", password = "Correct-password1" })
        };
        login.Headers.Add("X-CSRF-TOKEN", csrf!.Token);
        Assert.Equal(HttpStatusCode.OK, (await authenticatedClient.SendAsync(login)).StatusCode);

        var authenticatedNegotiate = await authenticatedClient.PostAsync(
            "/hubs/monitoring/negotiate?negotiateVersion=1",
            new StringContent(string.Empty));
        Assert.Equal(HttpStatusCode.OK, authenticatedNegotiate.StatusCode);
    }

    private sealed record CsrfResponse(string Token);

    private sealed class AuthFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            _connection.Open();
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                var descriptors = services.Where(item => item.ServiceType == typeof(DbContextOptions<NetworkMonitorDbContext>) || item.ServiceType == typeof(NetworkMonitorDbContext)).ToList();
                foreach (var descriptor in descriptors) services.Remove(descriptor);
                services.AddDbContext<NetworkMonitorDbContext>(options => options.UseSqlite(_connection));
            });
        }

        public async Task InitializeAsync()
        {
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<NetworkMonitorDbContext>().Database.EnsureCreatedAsync();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await users.CreateAsync(new ApplicationUser { UserName = "admin" }, "Correct-password1")).Succeeded);
            Assert.False((await users.CreateAsync(new ApplicationUser { UserName = "admin" }, "Another-password1")).Succeeded);
        }

        public override async ValueTask DisposeAsync() { await base.DisposeAsync(); await _connection.DisposeAsync(); }
    }
}
