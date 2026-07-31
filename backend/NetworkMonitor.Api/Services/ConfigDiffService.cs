using NetworkMonitor.Api.Dtos;

namespace NetworkMonitor.Api.Services;

public sealed class ConfigDiffService : IConfigDiffService
{
    public ConfigDiffResult Compare(string fromConfiguration, string toConfiguration)
    {
        var fromLines = SplitLines(fromConfiguration);
        var toLines = SplitLines(toConfiguration);
        var lcs = BuildLongestCommonSubsequenceTable(fromLines, toLines);
        var lines = new List<ConfigDiffLineResponse>(fromLines.Length + toLines.Length);
        var fromIndex = 0;
        var toIndex = 0;
        var addedLines = 0;
        var removedLines = 0;

        while (fromIndex < fromLines.Length && toIndex < toLines.Length)
        {
            if (string.Equals(fromLines[fromIndex], toLines[toIndex], StringComparison.Ordinal))
            {
                lines.Add(new ConfigDiffLineResponse(
                    ConfigDiffLineType.Unchanged,
                    fromIndex + 1,
                    toIndex + 1,
                    fromLines[fromIndex]));
                fromIndex++;
                toIndex++;
            }
            else if (lcs[fromIndex + 1, toIndex] >= lcs[fromIndex, toIndex + 1])
            {
                lines.Add(new ConfigDiffLineResponse(
                    ConfigDiffLineType.Removed,
                    fromIndex + 1,
                    null,
                    fromLines[fromIndex]));
                removedLines++;
                fromIndex++;
            }
            else
            {
                lines.Add(new ConfigDiffLineResponse(
                    ConfigDiffLineType.Added,
                    null,
                    toIndex + 1,
                    toLines[toIndex]));
                addedLines++;
                toIndex++;
            }
        }

        while (fromIndex < fromLines.Length)
        {
            lines.Add(new ConfigDiffLineResponse(
                ConfigDiffLineType.Removed,
                fromIndex + 1,
                null,
                fromLines[fromIndex++]));
            removedLines++;
        }

        while (toIndex < toLines.Length)
        {
            lines.Add(new ConfigDiffLineResponse(
                ConfigDiffLineType.Added,
                null,
                toIndex + 1,
                toLines[toIndex++]));
            addedLines++;
        }

        return new ConfigDiffResult(addedLines, removedLines, lines);
    }

    public static string NormalizeLineEndings(string configuration)
    {
        return configuration.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    }

    private static string[] SplitLines(string configuration)
    {
        return NormalizeLineEndings(configuration).Split('\n');
    }

    private static int[,] BuildLongestCommonSubsequenceTable(
        IReadOnlyList<string> fromLines,
        IReadOnlyList<string> toLines)
    {
        var table = new int[fromLines.Count + 1, toLines.Count + 1];
        for (var fromIndex = fromLines.Count - 1; fromIndex >= 0; fromIndex--)
        {
            for (var toIndex = toLines.Count - 1; toIndex >= 0; toIndex--)
            {
                table[fromIndex, toIndex] = string.Equals(
                    fromLines[fromIndex],
                    toLines[toIndex],
                    StringComparison.Ordinal)
                    ? table[fromIndex + 1, toIndex + 1] + 1
                    : Math.Max(table[fromIndex + 1, toIndex], table[fromIndex, toIndex + 1]);
            }
        }

        return table;
    }
}
