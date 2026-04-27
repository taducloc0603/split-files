namespace GapLogger.Services;

public static class MapNameHelper
{
    public static string BuildTradesMapName(string tickMap) => Build(tickMap, "_Trades");
    public static string BuildHistoryMapName(string tickMap) => Build(tickMap, "_History");

    private static string Build(string tickMap, string suffix)
    {
        if (string.IsNullOrWhiteSpace(tickMap)) return string.Empty;
        var n = tickMap.Trim();
        const string ts = "_Tick";
        return n.EndsWith(ts, StringComparison.OrdinalIgnoreCase)
            ? string.Concat(n.AsSpan(0, n.Length - ts.Length), suffix)
            : n + suffix;
    }
}