using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace NetworkMonitor.Api.Services;

public sealed class TcpPortProbe : ITcpPortProbe
{
    public async Task<TcpPortProbeResult> ProbeAsync(
        IPAddress address,
        int port,
        int timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        using var client = new TcpClient(address.AddressFamily);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromMilliseconds(timeoutMilliseconds));
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await client.ConnectAsync(new IPEndPoint(address, port), timeoutSource.Token);
            stopwatch.Stop();
            return new TcpPortProbeResult(true, stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new TcpPortProbeResult(false, null);
        }
        catch (SocketException)
        {
            return new TcpPortProbeResult(false, null);
        }
    }
}
