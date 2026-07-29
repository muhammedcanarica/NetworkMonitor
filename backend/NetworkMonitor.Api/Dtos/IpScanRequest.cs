using System.ComponentModel.DataAnnotations;

namespace NetworkMonitor.Api.Dtos;

public sealed class IpScanRequest
{
    public IpScanRequest()
    {
    }

    public IpScanRequest(string cidr)
    {
        Cidr = cidr;
    }

    [Required(AllowEmptyStrings = false)]
    public string Cidr { get; init; } = string.Empty;
}
