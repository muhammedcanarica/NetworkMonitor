using System.ComponentModel.DataAnnotations;
using System.Net;

namespace NetworkMonitor.Api.Dtos;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class ValidIpAddressAttribute : ValidationAttribute
{
    public ValidIpAddressAttribute()
        : base("The {0} field must contain a valid IPv4 or IPv6 address.")
    {
    }

    public override bool IsValid(object? value)
    {
        return value is string text && IPAddress.TryParse(text.Trim(), out _);
    }
}
