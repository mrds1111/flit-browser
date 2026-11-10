using System.Text.Json;

namespace Flit;

/// <summary>History browser (M3) — JSONL tunggal: %LOCALAPPDATA%\Flit\history.jsonl.</summary>
public static class HistoryStore
{
    public sealed record Entry(string Time, string Title, string Url);

    private static readonly object Lock = new();
    public static string FilePath => Path.Combine(AgentConfig.ConfigDir, "history.jsonl");

    public static void Append(string url, string title)
    {
        try
        {
            Directory.CreateDirectory(AgentConfig.ConfigDir);
            var line = JsonSerializer.Serialize(new
            {
                ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                title,
                url,
            });
            lock (Lock) File.AppendAllText(FilePath, line + Environment.NewLine);
        }
        catch { /* history tidak boleh bikin crash */ }
    }

    /// <summary>Baca entri terbaru (terlama dulu di file → dibalik). max = batas baris dari ekor.</summary>
    public static List<Entry> ReadLatest(int max = 5000)
    {
        var result = new List<Entry>();
        try
        {
            if (!File.Exists(FilePath)) return result;
            string[] lines;
            lock (Lock) lines = File.ReadAllLines(FilePath);
            int start = Math.Max(0, lines.Length - max);
            for (int i = lines.Length - 1; i >= start; i--)
            {
                try
                {
                    using var doc = JsonDocument.Parse(lines[i]);
                    var ts = doc.RootElement.GetProperty("ts").GetString() ?? "";
                    var title = doc.RootElement.GetProperty("title").GetString() ?? "";
                    var url = doc.RootElement.GetProperty("url").GetString() ?? "";
                    result.Add(new Entry(ts, title, url));
                }
                catch { /* baris korup → lewati */ }
            }
        }
        catch { }
        return result;
    }

    public static void Clear()
    {
        try { lock (Lock) File.Delete(FilePath); } catch { }
    }
}

/// <summary>Bookmark (M3) — array JSON: %LOCALAPPDATA%\Flit\bookmarks.json.</summary>
public static class BookmarksStore
{
    public sealed record Bookmark(string Url, string Title, string Added);

    private static readonly object Lock = new();
    private static List<Bookmark>? _cache;
    public static string FilePath => Path.Combine(AgentConfig.ConfigDir, "bookmarks.json");

    private static List<Bookmark> Load()
    {
        if (_cache is not null) return _cache;
        var list = new List<Bookmark>();
        try
        {
            if (File.Exists(FilePath))
            {
                var loaded = JsonSerializer.Deserialize<List<Bookmark>>(File.ReadAllText(FilePath));
                if (loaded is not null) list = loaded;
            }
        }
        catch { /* korup → mulai kosong */ }
        _cache = list;
        return list;
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(AgentConfig.ConfigDir);
            lock (Lock)
                File.WriteAllText(FilePath,
                    JsonSerializer.Serialize(_cache, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    public static bool IsBookmarked(string url) =>
        Load().Any(b => string.Equals(b.Url, url, StringComparison.OrdinalIgnoreCase));

    /// <summary>Toggle. Return: true = sekarang ter-bookmark.</summary>
    public static bool Toggle(string url, string title)
    {
        var list = Load();
        var existing = list.FindIndex(b => string.Equals(b.Url, url, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0)
        {
            list.RemoveAt(existing);
            Save();
            return false;
        }
        list.Add(new Bookmark(url, title, DateTime.Now.ToString("yyyy-MM-dd")));
        Save();
        return true;
    }

    public static List<Bookmark> All() => new(Load());
}
