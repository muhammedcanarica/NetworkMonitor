using System.ComponentModel.DataAnnotations;

namespace NetworkMonitor.Api.Dtos;

public sealed class UpdateInterfaceBandwidthThresholdRequest
{
    public double? InboundThresholdMbps { get; init; }
    public double? OutboundThresholdMbps { get; init; }

    [Range(1, 100)]
    public int BreachSampleCount { get; init; } = 3;

    [Range(1, 100)]
    public int RecoverySampleCount { get; init; } = 2;

    public bool IsEnabled { get; init; } = true;
}

public sealed record InterfaceBandwidthThresholdResponse(
    int InterfaceIndex,
    double? InboundThresholdMbps,
    double? OutboundThresholdMbps,
    int BreachSampleCount,
    int RecoverySampleCount,
    bool IsEnabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
