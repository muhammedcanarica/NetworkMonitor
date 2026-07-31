using System.ComponentModel.DataAnnotations;

namespace NetworkMonitor.Api.Dtos;

public sealed class DiscoverMonitoringInterfacesRequest
{
    [Range(1, int.MaxValue)]
    public int CredentialId { get; init; }

    [Range(500, 10000)]
    public int TimeoutMilliseconds { get; init; } = 2000;
}

public sealed class UpdateSnmpMonitoringRequest
{
    [Range(1, int.MaxValue)]
    public int CredentialId { get; init; }

    public bool IsEnabled { get; init; } = true;

    [Required]
    [MinLength(1)]
    [MaxLength(128)]
    public IReadOnlyList<int> InterfaceIndexes { get; init; } = [];
}

public sealed record SnmpMonitoredInterfaceResponse(
    int InterfaceIndex,
    string InterfaceName,
    string? Description,
    bool IsEnabled);

public sealed record SnmpMonitoringProfileResponse(
    int DeviceId,
    int CredentialId,
    bool IsEnabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<SnmpMonitoredInterfaceResponse> Interfaces);

public sealed record InterfaceTrafficSummaryResponse(
    int InterfaceIndex,
    string InterfaceName,
    string? Description,
    string? AdminStatus,
    string? OperStatus,
    DateTimeOffset? LastSampleAt,
    double? InboundBitsPerSecond,
    double? OutboundBitsPerSecond,
    InterfaceBandwidthThresholdResponse? Threshold,
    bool HasOpenInboundAlert,
    bool HasOpenOutboundAlert,
    bool HasActiveDownIncident);

public sealed record InterfaceTrafficSampleResponse(
    DateTimeOffset Timestamp,
    long InOctets,
    long OutOctets,
    double? InboundBitsPerSecond,
    double? OutboundBitsPerSecond,
    string OperStatus);

public sealed record InterfaceTrafficHistoryResponse(
    int InterfaceIndex,
    string InterfaceName,
    int Hours,
    IReadOnlyList<InterfaceTrafficSampleResponse> Samples);
