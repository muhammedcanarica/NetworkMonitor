namespace NetworkMonitor.Api.Dtos;

public sealed class WakeOnLanRequest
{
    public WakeOnLanRequest()
    {
    }

    public WakeOnLanRequest(string macAddress, string broadcastAddress, int port = 9)
    {
        MacAddress = macAddress;
        BroadcastAddress = broadcastAddress;
        Port = port;
    }

    public string MacAddress { get; init; } = string.Empty;

    public string BroadcastAddress { get; init; } = string.Empty;

    public int Port { get; init; } = 9;
}

public sealed record WakeOnLanResponse(
    string MacAddress,
    string BroadcastAddress,
    int Port,
    string Message);
