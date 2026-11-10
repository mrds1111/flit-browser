namespace Flit;

/// <summary>
/// Konfigurasi Flit (M2) — file JSON di %LOCALAPPDATA%\Flit\config.json.
/// Bagian yang dipakai malam ini: izin agent/MCP + S1 stealth dasar.
/// Kosong = nilai default. File tidak pernah ditimpa Flit (CLI hanya override runtime).
/// </summary>
public class AgentConfig
{
    /// <summary>Domain yang boleh diakses agent. KOSONG = semua diizinkan (default ramah);
    /// ada isi = whitelist (suffix match, "example.com" mencakup subdomain).</summary>
    public List<string> AllowedDomains { get; set; } = new();

    /// <summary>flit_evaluate_js mati secara default — nyalakan lewat config atau --allow-evaluate.</summary>
    public bool AllowEvaluate { get; set; } = false;

    /// <summary>S1: override User-Agent. Kosong = default WebView2 (tanpa jejak aneh).</summary>
    public string UserAgent { get; set; } = "";

    /// <summary>S1: bahasa engine (mis. "id-ID"). Kosong = default OS.</summary>
    public string Language { get; set; } = "";

    public static string ConfigDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Flit");

    public static string ConfigPath => Path.Combine(ConfigDir, "config.json");

    private static readonly System.Text.Json.JsonSerializerOptions Indent = new() { WriteIndented = true };

    public static AgentConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var cfg = System.Text.Json.JsonSerializer.Deserialize<AgentConfig>(File.ReadAllText(ConfigPath));
                if (cfg is not null) return cfg;
            }
        }
        catch { /* config rusak → default, jangan blok startup */ }
        return new AgentConfig();
    }

    /// <summary>Override dari command line (menang atas config file):</summary>
    public void ApplyArgs(string[] args)
    {
        foreach (var a in args)
        {
            if (a.StartsWith("--allow-domain=", StringComparison.Ordinal))
            {
                var d = a["--allow-domain=".Length..].Trim();
                if (d.Length > 0 && !AllowedDomains.Contains(d, StringComparer.OrdinalIgnoreCase))
                    AllowedDomains.Add(d);
            }
            else if (a == "--allow-evaluate") AllowEvaluate = true;
            else if (a.StartsWith("--ua=", StringComparison.Ordinal))
                UserAgent = a["--ua=".Length..];
            else if (a.StartsWith("--lang=", StringComparison.Ordinal))
                Language = a["--lang=".Length..];
        }
    }

    /// <summary>Gerbang domain: kosong = lolos; ada whitelist = host harus sama/subdomain.</summary>
    public bool IsDomainAllowed(string url)
    {
        if (AllowedDomains.Count == 0) return true;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return false;
        var host = u.Host.ToLowerInvariant();
        foreach (var d in AllowedDomains)
        {
            var dom = d.Trim().ToLowerInvariant();
            if (dom.Length == 0) continue;
            if (host == dom || host.EndsWith("." + dom, StringComparison.Ordinal)) return true;
        }
        return false;
    }
}
