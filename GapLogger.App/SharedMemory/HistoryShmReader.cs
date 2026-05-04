using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;

namespace GapLogger.SharedMemory;

public sealed class HistoryShmReader
{
    private const int HeaderSize = 16;
    private const int RecordSize = 124;
    private const int SymbolSize = 32;

    public IReadOnlyList<HistoryRecord> Read(string mapName)
    {
        if (string.IsNullOrWhiteSpace(mapName)) return Array.Empty<HistoryRecord>();
        var name = mapName.Trim();
        try
        {
            using var mmf = MemoryMappedFile.OpenExisting(name, MemoryMappedFileRights.Read);
            using var accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            var capacity = accessor.Capacity;
            if (capacity < HeaderSize) return Array.Empty<HistoryRecord>();

            var rawCount = accessor.ReadInt32(0);
            var safe = Math.Max(0, rawCount);
            var maxByCap = (int)Math.Max(0, (capacity - HeaderSize) / RecordSize);
            var count = Math.Min(safe, maxByCap);
            if (count == 0) return Array.Empty<HistoryRecord>();

            var list = new List<HistoryRecord>(count);
            var symBuf = new byte[SymbolSize];
            for (var i = 0; i < count; i++)
            {
                var off = HeaderSize + i * RecordSize;
                accessor.ReadArray(off + 92, symBuf, 0, SymbolSize);
                var endIdx = Array.IndexOf(symBuf, (byte)0);
                var symLen = endIdx >= 0 ? endIdx : SymbolSize;
                var symbol = Encoding.UTF8.GetString(symBuf, 0, symLen).Trim();

                list.Add(new HistoryRecord(
                    Ticket: accessor.ReadUInt64(off + 0),
                    Symbol: symbol,
                    TradeType: accessor.ReadInt32(off + 8),
                    Volume: accessor.ReadDouble(off + 12),
                    OpenPrice: accessor.ReadDouble(off + 20),
                    ClosePrice: accessor.ReadDouble(off + 28),
                    Sl: accessor.ReadDouble(off + 36),
                    Tp: accessor.ReadDouble(off + 44),
                    Commission: accessor.ReadDouble(off + 52),
                    Profit: accessor.ReadDouble(off + 60),
                    OpenTimeMsc: accessor.ReadUInt64(off + 68),
                    CloseTimeMsc: accessor.ReadUInt64(off + 76),
                    CloseEaTimeLocal: accessor.ReadUInt64(off + 84)));
            }
            return list;
        }
        catch (FileNotFoundException) { return Array.Empty<HistoryRecord>(); }
        catch { return Array.Empty<HistoryRecord>(); }
    }
}
