using Microsoft.AspNetCore.Identity;
using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Services;

public static class AdminBootstrapper
{
    public static async Task BootstrapAsync(IServiceProvider services)
    {
        var username = Environment.GetEnvironmentVariable("NETSCOPE_ADMIN_USERNAME");
        var password = Environment.GetEnvironmentVariable("NETSCOPE_ADMIN_PASSWORD");
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)) return;
        using var scope = services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if (await users.FindByNameAsync(username) is not null) return;
        var result = await users.CreateAsync(new ApplicationUser { UserName = username }, password);
        if (!result.Succeeded) throw new InvalidOperationException("Admin bootstrap failed. Check the configured username and password policy.");
    }
}
