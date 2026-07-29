namespace NetworkMonitor.Api.Models;

public static class PingFailureReasons
{
    public const string Timeout = "Timeout";
    public const string DestinationHostUnreachable = "DestinationHostUnreachable";
    public const string DestinationNetworkUnreachable = "DestinationNetworkUnreachable";
    public const string DestinationPortUnreachable = "DestinationPortUnreachable";
    public const string DestinationProtocolUnreachable = "DestinationProtocolUnreachable";
    public const string PacketTooBig = "PacketTooBig";
    public const string TtlExpired = "TtlExpired";
    public const string BadDestination = "BadDestination";
    public const string Unknown = "Unknown";

    public static string Normalize(string? failureReason)
    {
        return failureReason switch
        {
            Timeout => Timeout,
            DestinationHostUnreachable => DestinationHostUnreachable,
            DestinationNetworkUnreachable => DestinationNetworkUnreachable,
            DestinationPortUnreachable => DestinationPortUnreachable,
            DestinationProtocolUnreachable => DestinationProtocolUnreachable,
            PacketTooBig => PacketTooBig,
            TtlExpired => TtlExpired,
            BadDestination => BadDestination,
            _ => Unknown
        };
    }
}
