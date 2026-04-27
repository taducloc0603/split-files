using GapLogger.SharedMemory;

namespace GapLogger.Services;

public sealed class GapCalculator
{
    private readonly int _point;

    public GapCalculator()
    {
        var raw = Environment.GetEnvironmentVariable("GAP_LOGGER_POINT");
        _point = int.TryParse(raw, out var p) && p > 0 ? p : 100000;
    }

    public int Point => _point;

    public (int? GapBuy, int? GapSell) Calculate(TickRecord? a, TickRecord? b)
    {
        if (a is null || b is null) return (null, null);
        var buy = (int)((b.Bid * _point) - (a.Ask * _point));
        var sell = (int)((b.Ask * _point) - (a.Bid * _point));
        return (buy, sell);
    }
}