namespace Flit;

/// <summary>
/// UserScript ringan (M6) — pengganti praktis extension (WebView2 tidak mendukung
/// Chromium extension: argumen --load-extension lolos tapi engine menolak memuatnya,
/// terverifikasi uji M6). File *.js di %LOCALAPPDATA%\Flit\userscripts dengan header:
///
///   // ==UserScript==
///   // @match example.com        (atau *.example.com, atau URL penuh)
///   // ==/UserScript==
///
/// Tanpa @match = jalan di semua situs. Dibaca ulang dari disk tiap WebView2 baru
/// (jumlah file diasumsikan kecil — biar edit langsung tanpa restart).
/// </summary>
public static class UserScripts
{
    public static string Dir => Path.Combine(AgentConfig.ConfigDir, "userscripts");

    public static List<(string Script, List<string> Matches)> Load()
    {
        var result = new List<(string, List<string>)>();
        try
        {
            if (!Directory.Exists(Dir)) return result;
            foreach (var f in Directory.GetFiles(Dir, "*.js").OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    var text = File.ReadAllText(f);
                    var matches = new List<string>();
                    foreach (var line in text.Split('\n').Take(30))
                    {
                        var t = line.Trim();
                        if (t.StartsWith("// @match", StringComparison.Ordinal))
                            matches.Add(t.Substring("// @match".Length).Trim());
                        else if (t.StartsWith("// ==/UserScript==", StringComparison.Ordinal)) break;
                    }
                    result.Add((text, matches));
                }
                catch { /* satu file rusak ≠ semua mati */ }
            }
        }
        catch { }
        return result;
    }

    /// <summary>Cocokkan pola @match sederhana terhadap host: "host", "*.host", atau URL penuh.</summary>
    public static bool Matches(List<string> matches, string host)
    {
        if (matches.Count == 0) return true;
        if (host.Length == 0) return false;
        foreach (var m in matches)
        {
            var pat = m.Trim().ToLowerInvariant();
            if (Uri.TryCreate(pat, UriKind.Absolute, out var u)) pat = u.Host;
            if (pat.StartsWith("*.")) pat = pat[2..];
            if (pat.Length == 0) return true;
            if (host.Equals(pat, StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith("." + pat, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
