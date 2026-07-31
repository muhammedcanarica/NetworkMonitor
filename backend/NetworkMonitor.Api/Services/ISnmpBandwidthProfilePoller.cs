namespace NetworkMonitor.Api.Services;

public interface ISnmpBandwidthProfilePoller
{
    Task PollAsync(int profileId, CancellationToken cancellationToken);
}
