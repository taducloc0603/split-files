using GapLogger.Models;
using GapLogger.SharedMemory;

namespace GapLogger.Services;

public sealed class OrderEventDetector
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private readonly TradesShmReader _trades;
    private CancellationTokenSource? _cts;
    private Task? _worker;
    private readonly object _lock = new();

    public OrderEventDetector(TradesShmReader trades) { _trades = trades; }
    public bool IsRunning { get; private set; }

    public Task StartAsync(string tickMapA, string tickMapB, Action<OrderEvent> onEvent, Action<string>? onError = null)
    {
        lock (_lock)
        {
            if (IsRunning) return Task.CompletedTask;
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            var tradeMapA = MapNameHelper.BuildTradesMapName(tickMapA);
            var tradeMapB = MapNameHelper.BuildTradesMapName(tickMapB);
            var histMapA = MapNameHelper.BuildHistoryMapName(tickMapA);
            var histMapB = MapNameHelper.BuildHistoryMapName(tickMapB);
            _worker = Task.Run(() => LoopAsync(tradeMapA, tradeMapB, histMapA, histMapB, onEvent, onError, ct));
            IsRunning = true;
        }
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        Task? worker;
        lock (_lock)
        {
            if (!IsRunning) return;
            _cts?.Cancel();
            worker = _worker;
            _worker = null;
            IsRunning = false;
        }
        if (worker is not null)
        {
            try { await worker; } catch (OperationCanceledException) { }
        }
        lock (_lock) { _cts?.Dispose(); _cts = null; }
    }

    private async Task LoopAsync(string tradeMapA, string tradeMapB, string histMapA, string histMapB,
        Action<OrderEvent> onEvent, Action<string>? onError, CancellationToken ct)
    {
        var openA = new HashSet<ulong>();
        var openB = new HashSet<ulong>();
        var primedA = false;
        var primedB = false;
        using var timer = new PeriodicTimer(PollInterval);

        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                Diff("A", tradeMapA, histMapA, ref openA, ref primedA, onEvent);
                Diff("B", tradeMapB, histMapB, ref openB, ref primedB, onEvent);
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex.Message);
            }
        }
    }

    private void Diff(string side, string mapName, string histMapName, ref HashSet<ulong> known, ref bool primed, Action<OrderEvent> onEvent)
    {
        var records = _trades.Read(mapName);
        var byTicket = records.ToDictionary(r => r.Ticket);
        var current = byTicket.Keys.ToHashSet();
        if (!primed)
        {
            known = current;
            primed = true;
            return;
        }
        var now = DateTime.UtcNow;
        foreach (var newTicket in current.Except(known))
        {
            byTicket.TryGetValue(newTicket, out var rec);
            onEvent(new OrderEvent(side, "Open", newTicket, now,
                rec?.TradeType == 0 ? "BUY" : "SELL",
                rec?.TimeMsc ?? 0,
                null));
        }
        if (known.Except(current).Any())
        {
            var history = _trades.Read(histMapName).ToDictionary(r => r.Ticket);
            foreach (var goneTicket in known.Except(current))
            {
                history.TryGetValue(goneTicket, out var hist);
                onEvent(new OrderEvent(side, "Close", goneTicket, now,
                    hist?.TradeType == 0 ? "BUY" : "SELL",
                    hist?.TimeMsc ?? 0,
                    hist?.Profit));
            }
        }
        known = current;
    }
}