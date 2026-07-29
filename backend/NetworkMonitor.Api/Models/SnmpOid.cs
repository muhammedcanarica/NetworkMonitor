namespace NetworkMonitor.Api.Models;

public static class SnmpOid
{
    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim().TrimStart('.');
        var parts = text.Split('.');
        if (parts.Length < 2 || parts.Length > 128)
        {
            return false;
        }

        var arcs = new uint[parts.Length];
        for (var index = 0; index < parts.Length; index++)
        {
            if (parts[index].Length == 0 || !uint.TryParse(parts[index], out arcs[index]))
            {
                return false;
            }
        }

        if (arcs[0] > 2 || (arcs[0] < 2 && arcs[1] > 39))
        {
            return false;
        }

        normalized = string.Join('.', arcs);
        return true;
    }
}
