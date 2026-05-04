using System.Globalization;
using System.IO;
using GapLogger.Models;
using GapLogger.SharedMemory;

namespace GapLogger.Services;

public sealed class GapLoggingSession
{
    private readonly TickShmReader _tickReader;
    private readonly OrderEventDetector _detector;
    private readonly GapCalculator _gapCalc;
    private readonly object _lock = new();

    private CsvFileWriter? _wTickA, _wTickB, _wGap, _wEvents;
    private long _lastTsA, _lastTsB;
    private GapSnapshot _latest;
    private bool _isRunning;
    private string? _folderPath;
    private string? _lastError;

    public GapLoggingSession(TickShmReader tickReader, OrderEventDetector detector, GapCalculator gapCalc)
    {
        _tickReader = tickReader;
        _detector = detector;
        _gapCalc = gapCalc;
    }

    public bool IsRunning { get { lock (_lock) return _isRunning; } }
    public string? FolderPath { get { lock (_lock) return _folderPath; } }
    public string? LastError { get { lock (_lock) return _lastError; } }
    public int TickARowCount;
    public int TickBRowCount;
    public int GapRowCount;
    public int EventRowCount;

    public async Task StartAsync(string mapA, string mapB)
    {
        lock (_lock)
        {
            if (_isRunning) return;
            _isRunning = true;
            _lastTsA = 0; _lastTsB = 0;
            TickARowCount = TickBRowCount = GapRowCount = EventRowCount = 0;
            _lastError = null;
        }

        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var folder = Path.Combine(desktop, "check-gap");
        Directory.CreateDirectory(folder);
        var suffix = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);

        var tickHeader = new[] { "timestamp", "symbol", "bid", "ask", "spread", "tickTimeMsc", "version", "error" };
        var gapHeader = new[] { "timestamp", "gapBuy", "gapSell", "bidA", "askA", "bidB", "askB", "errorA", "errorB" };
        var evHeader = new[] { "timestamp", "ticket", "type", "serverTime", "tradeType", "profit", "gapBuy", "gapSell", "bidA", "askA", "bidB", "askB" };

        _wTickA = new CsvFileWriter(Path.Combine(folder, $"{suffix}_tickA.csv"), tickHeader);
        _wTickB = new CsvFileWriter(Path.Combine(folder, $"{suffix}_tickB.csv"), tickHeader);
        _wGap = new CsvFileWriter(Path.Combine(folder, $"{suffix}_gap.csv"), gapHeader);
        _wEvents = new CsvFileWriter(Path.Combine(folder, $"{suffix}_orderEvents.csv"), evHeader);

        lock (_lock) _folderPath = folder;

        _tickReader.SnapshotReceived += OnSnapshot;
        await _tickReader.StartAsync(mapA, mapB);
        await _detector.StartAsync(mapA, mapB, OnOrderEvent, err => { lock (_lock) _lastError = err; });
    }

    public async Task StopAsync()
    {
        lock (_lock)
        {
            if (!_isRunning) return;
            _isRunning = false;
        }
        _tickReader.SnapshotReceived -= OnSnapshot;
        await _tickReader.StopAsync();
        await _detector.StopAsync();
        lock (_lock)
        {
            _wTickA?.Dispose(); _wTickA = null;
            _wTickB?.Dispose(); _wTickB = null;
            _wGap?.Dispose(); _wGap = null;
            _wEvents?.Dispose(); _wEvents = null;
        }
    }

    private void OnSnapshot(object? sender, TickSnapshot snap)
    {
        try
        {
            var ts = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
            var (gapBuy, gapSell) = _gapCalc.Calculate(snap.A, snap.B);

            var newSnap = new GapSnapshot(gapBuy, gapSell, snap.A?.Bid, snap.A?.Ask, snap.B?.Bid, snap.B?.Ask);
            lock (_lock) _latest = newSnap;

            bool tickAChanged = snap.A is not null && snap.A.TimestampMs != _lastTsA;
            bool tickBChanged = snap.B is not null && snap.B.TimestampMs != _lastTsB;
            bool errorA = snap.A is null && !string.IsNullOrEmpty(snap.ErrorA);
            bool errorB = snap.B is null && !string.IsNullOrEmpty(snap.ErrorB);

            if (tickAChanged || tickBChanged || errorA || errorB)
            {
                _wGap?.WriteRow(ts, gapBuy, gapSell, snap.A?.Bid, snap.A?.Ask, snap.B?.Bid, snap.B?.Ask, snap.ErrorA, snap.ErrorB);
                Interlocked.Increment(ref GapRowCount);
            }

            if (tickAChanged)
            {
                _lastTsA = snap.A!.TimestampMs;
                _wTickA?.WriteRow(ts, snap.A.Symbol, snap.A.Bid, snap.A.Ask, snap.A.Spread, snap.A.TickTimeMsc, snap.A.Version, "");
                Interlocked.Increment(ref TickARowCount);
            }
            else if (errorA)
            {
                _wTickA?.WriteRow(ts, "", "", "", "", "", "", snap.ErrorA);
                Interlocked.Increment(ref TickARowCount);
            }

            if (tickBChanged)
            {
                _lastTsB = snap.B!.TimestampMs;
                _wTickB?.WriteRow(ts, snap.B.Symbol, snap.B.Bid, snap.B.Ask, snap.B.Spread, snap.B.TickTimeMsc, snap.B.Version, "");
                Interlocked.Increment(ref TickBRowCount);
            }
            else if (errorB)
            {
                _wTickB?.WriteRow(ts, "", "", "", "", "", "", snap.ErrorB);
                Interlocked.Increment(ref TickBRowCount);
            }
        }
        catch (Exception ex)
        {
            lock (_lock) _lastError = ex.Message;
        }
    }

    private void OnOrderEvent(OrderEvent ev)
    {
        try
        {
            GapSnapshot snap;
            lock (_lock) snap = _latest;
            var ts = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
            var serverTimeStr = ev.ServerTimeMsc > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds((long)ev.ServerTimeMsc).UtcDateTime.ToString("o", CultureInfo.InvariantCulture)
                : "";
            object? profitVal = ev.Profit.HasValue ? ev.Profit.Value : null;
            _wEvents?.WriteRow(ts, ev.Ticket, ev.Kind, serverTimeStr, ev.TradeType, profitVal,
                snap.GapBuy, snap.GapSell, snap.BidA, snap.AskA, snap.BidB, snap.AskB);
            Interlocked.Increment(ref EventRowCount);
        }
        catch (Exception ex)
        {
            lock (_lock) _lastError = ex.Message;
        }
    }
}

public readonly record struct GapSnapshot(int? GapBuy, int? GapSell, double? BidA, double? AskA, double? BidB, double? AskB);