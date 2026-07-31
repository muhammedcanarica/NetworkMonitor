using Microsoft.EntityFrameworkCore;
using NetworkMonitor.Api.Data;
using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Services;

public sealed class NetworkCredentialService(NetworkMonitorDbContext dbContext, ISecretProtector secretProtector) : INetworkCredentialService
{
    public async Task<NetworkCredentialResponse> CreateAsync(CreateNetworkCredentialRequest request, CancellationToken token)
    {
        await ValidateAsync(request.Name, request.Type, request.Username, request.Secret, request.DeviceId, true, token);
        var now = DateTimeOffset.UtcNow;
        var entity = new NetworkCredential { Name = request.Name.Trim(), Type = request.Type, Username = NormalizeUsername(request.Type, request.Username), ProtectedSecret = secretProtector.Protect(request.Secret), DeviceId = request.DeviceId, CreatedAt = now, UpdatedAt = now };
        dbContext.NetworkCredentials.Add(entity); await dbContext.SaveChangesAsync(token); return Map(entity);
    }

    public async Task<IReadOnlyList<NetworkCredentialResponse>> ListAsync(CancellationToken token) => await dbContext.NetworkCredentials.AsNoTracking().OrderBy(item => item.Name).Select(item => new NetworkCredentialResponse(item.Id, item.Name, item.Type, item.Username, item.DeviceId, item.CreatedAt, item.UpdatedAt, item.ProtectedSecret != "")).ToListAsync(token);

    public async Task<NetworkCredentialResponse> UpdateAsync(int id, UpdateNetworkCredentialRequest request, CancellationToken token)
    {
        var entity = await dbContext.NetworkCredentials.SingleOrDefaultAsync(item => item.Id == id, token) ?? throw new KeyNotFoundException("Credential was not found.");
        await ValidateAsync(request.Name, request.Type, request.Username, request.Secret, request.DeviceId, false, token);
        entity.Name = request.Name.Trim(); entity.Type = request.Type; entity.Username = NormalizeUsername(request.Type, request.Username); entity.DeviceId = request.DeviceId; entity.UpdatedAt = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.Secret)) entity.ProtectedSecret = secretProtector.Protect(request.Secret);
        await dbContext.SaveChangesAsync(token); return Map(entity);
    }

    public async Task DeleteAsync(int id, CancellationToken token) { var entity = await dbContext.NetworkCredentials.SingleOrDefaultAsync(item => item.Id == id, token) ?? throw new KeyNotFoundException("Credential was not found."); dbContext.Remove(entity); await dbContext.SaveChangesAsync(token); }

    public async Task<(NetworkCredentialType Type, string? Username, string Secret)> ResolveAsync(int id, CancellationToken token) { var entity = await dbContext.NetworkCredentials.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, token) ?? throw new KeyNotFoundException("Credential was not found."); return (entity.Type, entity.Username, secretProtector.Unprotect(entity.ProtectedSecret)); }

    private async Task ValidateAsync(string name, NetworkCredentialType type, string? username, string? secret, int? deviceId, bool requireSecret, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Credential name is required.");
        if (!Enum.IsDefined(type)) throw new ArgumentException("Credential type is invalid.");
        if (requireSecret && string.IsNullOrWhiteSpace(secret)) throw new ArgumentException("Secret is required.");
        if (type == NetworkCredentialType.SshPassword && string.IsNullOrWhiteSpace(username)) throw new ArgumentException("Username is required for SSH credentials.");
        if (deviceId.HasValue && !await dbContext.Devices.AnyAsync(device => device.Id == deviceId, token)) throw new ArgumentException("Associated device was not found.");
    }

    private static string? NormalizeUsername(NetworkCredentialType type, string? username) => type == NetworkCredentialType.SshPassword ? username?.Trim() : null;
    private static NetworkCredentialResponse Map(NetworkCredential item) => new(item.Id, item.Name, item.Type, item.Username, item.DeviceId, item.CreatedAt, item.UpdatedAt, true);
}
