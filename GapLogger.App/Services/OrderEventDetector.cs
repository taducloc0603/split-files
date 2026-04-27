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
            _worker = Task.Run(() => LoopAsync(tradeMapA, tradeMapB, onEvent, onError, ct));
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

    private async Task LoopAsync(string tradeMapA, string tradeMapB,
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
                Diff("A", tradeMapA, ref openA, ref primedA, onEvent);
                Diff("B", tradeMapB, ref openB, ref primedB, onEvent);
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex.Message);
            }
        }
    }

    private void Diff(string side, string mapName, ref HashSet<ulong> known, ref bool primed, Action<OrderEvent> onEvent)
    {
        var current = new HashSet<ulong>(_trades.Read(mapName).Select(r => r.Ticket));
        if (!primed)
        {
            known = current;
            primed = true;
            return;
        }
        var now = DateTime.UtcNow;
        foreach (var newTicket in current.Except(known))
            onEvent(new OrderEvent(side, "Open", newTicket, now));
        foreach (var goneTicket in known.Except(current))
            onEvent(new OrderEvent(side, "Close", goneTicket, now));
        known = current;
    }
}