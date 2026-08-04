using NetworkMonitor.Api.Dtos;

namespace NetworkMonitor.Api.Services;

public sealed class ConfigBackupProviderResolver(IEnumerable<IConfigBackupProvider> providers)
{
    private readonly IReadOnlyDictionary<ConfigBackupVendor, IConfigBackupProvider> _providers =
        providers.ToDictionary(provider => provider.Vendor);

    public IConfigBackupProvider Resolve(ConfigBackupVendor vendor)
    {
        if (_providers.TryGetValue(vendor, out var provider))
        {
            return provider;
        }

        var message = vendor == ConfigBackupVendor.Fortinet
            ? "Fortinet provider not implemented yet."
            : $"Configuration backup provider '{vendor}' is not supported.";
        throw new ConfigBackupValidationException(message);
    }
}
