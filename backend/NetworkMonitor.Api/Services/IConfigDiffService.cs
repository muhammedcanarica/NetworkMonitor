using NetworkMonitor.Api.Dtos;

namespace NetworkMonitor.Api.Services;

public interface IConfigDiffService
{
    ConfigDiffResult Compare(string fromConfiguration, string toConfiguration);
}

public sealed record ConfigDiffResult(
    int AddedLines,
    int RemovedLines,
    IReadOnlyList<ConfigDiffLineResponse> Lines);
