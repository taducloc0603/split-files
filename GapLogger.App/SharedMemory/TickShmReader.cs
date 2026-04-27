using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;

namespace GapLogger.SharedMemory;

public sealed class TickShmReader
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);
    private const int ExpectedVersion = 1;
    private const long VersionOffset = 0;
    private const long TimestampMsOffset = 4;
    private const long BidOffset = 16;
    private const long AskOffset = 24;
    private const long SpreadOffset = 32;
    private const long TickTimeMscOffset = 40;
    private const long SymbolOffset = 48;
    private const int MaxSymbolBytes = 64;

    private CancellationTokenSource? _cts;
    private Task? _worker;
    private string? _mapA;
    private string? _mapB;
    private MapHandle? _handleA;
    private MapHandle? _handleB;
    private readonly object _lock = new();

    public event EventHandler<TickSnapshot>? SnapshotReceived;
    public bool IsRunning { get; private set; }

    public static MapProbeResult Probe(string mapName)
    {
        var name = mapName?.Trim();
        if (string.IsNullOrEmpty(name))
            return new MapProbeResult(MapProbeStatus.Invalid, "Map name rỗng", null);
        try
        {
            using var mmf = MemoryMappedFile.OpenExisting(name, MemoryMappedFileRights.Read);
            using var accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            var (record, error) = TryParse(accessor);
            return record is not null
                ? new MapProbeResult(MapProbeStatus.Ok, null, record.Symbol)
                : new MapProbeResult(MapProbeStatus.Invalid, error, null);
        }
        catch (FileNotFoundException)
        {
            return new MapProbeResult(MapProbeStatus.NotFound, null, null);
        }
        catch (Exception ex)
        {
            return new MapProbeResult(MapProbeStatus.Invalid, ex.Message, null);
        }
    }

    public Task StartAsync(string mapA, string mapB)
    {
        lock (_lock)
        {
            if (IsRunning) return Task.CompletedTask;
            _mapA = mapA?.Trim();
            _mapB = mapB?.Trim();
            _cts = new CancellationTokenSource();
            _worker = Task.Run(() => LoopAsync(_cts.Token));
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
        DisposeHandles();
        lock (_lock) { _cts?.Dispose(); _cts = null; }
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(ct))
        {
            var (a, errA) = ReadOne(_mapA, ref _handleA);
            var (b, errB) = ReadOne(_mapB, ref _handleB);
            SnapshotReceived?.Invoke(this, new TickSnapshot(a, b, errA, errB, DateTime.UtcNow));
        }
    }

    private (TickRecord?, string?) ReadOne(string? mapName, ref MapHandle? handle)
    {
        if (string.IsNullOrWhiteSpace(mapName)) return (null, "Map name rỗng");
        try
        {
            handle ??= OpenMap(mapName);
            return TryParse(handle.Accessor);
        }
        catch (FileNotFoundException)
        {
            handle?.Dispose(); handle = null;
            return (null, $"Map không tồn tại: {mapName}");
        }
        catch (Exception ex)
        {
            handle?.Dispose(); handle = null;
            return (null, ex.Message);
        }
    }

    private static MapHandle OpenMap(string mapName)
    {
        var mmf = MemoryMappedFile.OpenExisting(mapName, MemoryMappedFileRights.Read);
        var accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
        return new MapHandle(mmf, accessor);
    }

    private static (TickRecord?, string?) TryParse(MemoryMappedViewAccessor accessor)
    {
        var capacity = accessor.Capacity;
        if (capacity <= SymbolOffset) return (null, "Buffer quá ngắn");

        var version = accessor.ReadInt32(VersionOffset);
        if (version != ExpectedVersion) return (null, $"Version sai: {version}");

        var ts = accessor.ReadInt64(TimestampMsOffset);
        if (ts <= 0) return (null, "TimestampMs invalid");

        var bid = accessor.ReadDouble(BidOffset);
        var ask = accessor.ReadDouble(AskOffset);
        var spread = accessor.ReadDouble(SpreadOffset);
        var tickTime = accessor.ReadInt64(TickTimeMscOffset);

        if (double.IsNaN(bid) || double.IsInfinity(bid) || bid <= 0 ||
            double.IsNaN(ask) || double.IsInfinity(ask) || ask <= 0 ||
            double.IsNaN(spread) || double.IsInfinity(spread))
            return (null, "Bid/Ask/Spread invalid");

        var available = capacity - SymbolOffset;
        var len = (int)Math.Min(available, MaxSymbolBytes);
        var bytes = new byte[len];
        accessor.ReadArray(SymbolOffset, bytes, 0, len);
        var nullIdx = Array.IndexOf(bytes, (byte)0);
        var symLen = nullIdx >= 0 ? nullIdx : len;
        if (symLen <= 0) return (null, "Symbol rỗng");
        var symbol = Encoding.ASCII.GetString(bytes, 0, symLen).Trim();
        if (string.IsNullOrEmpty(symbol)) return (null, "Symbol rỗng");

        return (new TickRecord(version, ts, bid, ask, spread, tickTime, symbol), null);
    }

    private void DisposeHandles()
    {
        lock (_lock)
        {
            _handleA?.Dispose(); _handleA = null;
            _handleB?.Dispose(); _handleB = null;
        }
    }

    private sealed class MapHandle(MemoryMappedFile mmf, MemoryMappedViewAccessor accessor) : IDisposable
    {
        public MemoryMappedFile Mmf { get; } = mmf;
        public MemoryMappedViewAccessor Accessor { get; } = accessor;
        public void Dispose() { Accessor.Dispose(); Mmf.Dispose(); }
    }
}

public enum MapProbeStatus { Ok, NotFound, Invalid }

public sealed record MapProbeResult(MapProbeStatus Status, string? Error, string? Symbol);