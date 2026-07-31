using NetworkMonitor.Api.Dtos;
using NetworkMonitor.Api.Models;

namespace NetworkMonitor.Api.Services;

public interface INetworkCredentialService
{
    Task<NetworkCredentialResponse> CreateAsync(CreateNetworkCredentialRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<NetworkCredentialResponse>> ListAsync(CancellationToken cancellationToken);
    Task<NetworkCredentialResponse> UpdateAsync(int id, UpdateNetworkCredentialRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
    Task<(NetworkCredentialType Type, string? Username, string Secret)> ResolveAsync(int id, CancellationToken cancellationToken);
}
