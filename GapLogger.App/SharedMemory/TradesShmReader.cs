using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;

namespace GapLogger.SharedMemory;

public sealed class TradesShmReader
{
    private const int HeaderSize = 16;
    private const int RecordSize = 100;
    private const int SymbolSize = 32;

    public IReadOnlyList<TradeRecord> Read(string mapName)
    {
        if (string.IsNullOrWhiteSpace(mapName)) return Array.Empty<TradeRecord>();
        var name = mapName.Trim();
        try
        {
            using var mmf = MemoryMappedFile.OpenExisting(name, MemoryMappedFileRights.Read);
            using var accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            var capacity = accessor.Capacity;
            if (capacity < HeaderSize) return Array.Empty<TradeRecord>();

            var rawCount = accessor.ReadInt32(0);
            var safe = Math.Max(0, rawCount);
            var maxByCap = (int)Math.Max(0, (capacity - HeaderSize) / RecordSize);
            var count = Math.Min(safe, maxByCap);
            if (count == 0) return Array.Empty<TradeRecord>();

            var list = new List<TradeRecord>(count);
            var symBuf = new byte[SymbolSize];
            for (var i = 0; i < count; i++)
            {
                var off = HeaderSize + i * RecordSize;
                accessor.ReadArray(off + 68, symBuf, 0, SymbolSize);
                var endIdx = Array.IndexOf(symBuf, (byte)0);
                var symLen = endIdx >= 0 ? endIdx : SymbolSize;
                var symbol = Encoding.UTF8.GetString(symBuf, 0, symLen).Trim();

                list.Add(new TradeRecord(
                    Ticket: accessor.ReadUInt64(off + 0),
                    Symbol: symbol,
                    TradeType: accessor.ReadInt32(off + 48),
                    Lot: accessor.ReadDouble(off + 8),
                    Price: accessor.ReadDouble(off + 16),
                    Sl: accessor.ReadDouble(off + 24),
                    Tp: accessor.ReadDouble(off + 32),
                    Profit: accessor.ReadDouble(off + 40),
                    TimeMsc: accessor.ReadUInt64(off + 52),
                    OpenEaTimeLocal: accessor.ReadUInt64(off + 60)));
            }
            return list;
        }
        catch (FileNotFoundException) { return Array.Empty<TradeRecord>(); }
        catch { return Array.Empty<TradeRecord>(); }
    }
}