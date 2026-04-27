using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace GapLogger.Services;

public static class MapNamePersistence
{
    private sealed class Settings
    {
        public string? mapNameA { get; set; }
        public string? mapNameB { get; set; }
    }

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GapLogger",
        "settings.json");

    public static (string A, string B) Load()
    {
        try
        {
            var path = SettingsPath;
            if (!File.Exists(path)) return ("", "");
            var json = File.ReadAllText(path);
            var s = JsonSerializer.Deserialize<Settings>(json);
            return (s?.mapNameA ?? "", s?.mapNameB ?? "");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MapNamePersistence] Load failed: {ex.Message}");
            return ("", "");
        }
    }

    public static void Save(string a, string b)
    {
        try
        {
            var path = SettingsPath;
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(new Settings { mapNameA = a, mapNameB = b });
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MapNamePersistence] Save failed: {ex.Message}");
        }
    }
}
