using System.Globalization;
using System.IO;
using System.Text;

namespace GapLogger.Services;

public sealed class CsvFileWriter : IDisposable
{
    private readonly StreamWriter _w;
    private readonly object _lock = new();
    private bool _disposed;

    public CsvFileWriter(string path, string[] header)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        _w = new StreamWriter(path, append: false, new UTF8Encoding(false)) { AutoFlush = false };
        _w.WriteLine(string.Join(",", header));
        _w.Flush();
    }

    public void WriteRow(params object?[] cells)
    {
        lock (_lock)
        {
            if (_disposed) return;
            _w.WriteLine(string.Join(",", cells.Select(Format)));
            _w.Flush();
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            try { _w.Flush(); } catch { }
            _w.Dispose();
        }
    }

    private static string Format(object? v) => v switch
    {
        null => "",
        double d => d.ToString("G17", CultureInfo.InvariantCulture),
        decimal m => m.ToString(CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => (v.ToString() ?? "").Replace(',', ';')
    };
}