using Microsoft.AspNetCore.DataProtection;

namespace NetworkMonitor.Api.Services;

public sealed class DataProtectionSecretProtector(IDataProtectionProvider provider) : ISecretProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("NetworkMonitor.NetworkCredential.v1");
    public string Protect(string secret) => _protector.Protect(secret);
    public string Unprotect(string protectedSecret) => _protector.Unprotect(protectedSecret);
}
