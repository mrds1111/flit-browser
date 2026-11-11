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

    /// <summary>S2: zona waktu IANA (mis. "Asia/Jakarta"). Kosong = native. Butuh --ua terpasang.</summary>
    public string Timezone { get; set; } = "";

    /// <summary>S2: paksa navigator.platform (mis. "MacIntel"). Kosong = diturunkan dari UA.</summary>
    public string Platform { get; set; } = "";

    /// <summary>S2: navigator.hardwareConcurrency. Default 8.</summary>
    public int HardwareConcurrency { get; set; } = 8;

    /// <summary>S2: navigator.deviceMemory (GiB, Chrome melaporkan 2/4/8). Default 8.</summary>
    public int DeviceMemory { get; set; } = 8;

    /// <summary>M4: proxy Chromium, mis. "http://127.0.0.1:8888". Kosong = tanpa proxy.</summary>
    public string Proxy { get; set; } = "";

    /// <summary>M4: nama profil — folder data (cookie/riwayat/cache) terpisah %LOCALAPPDATA%\Flit\profiles\&lt;nama&gt;.</summary>
    public string Profile { get; set; } = "";

    /// <summary>M4: tool MCP destruktif (close tab, evaluate) wajib membawa confirm:true.</summary>
    public bool RequireConfirm { get; set; } = false;

    /// <summary>M6: buka lagi tab sesi terakhir saat start (mode jendela normal).</summary>
    public bool RestoreSession { get; set; } = true;

    /// <summary>M6: tab latar tidur setelah N detik (live via Settings).</summary>
    public int SleepAfterSec { get; set; } = 60;

    /// <summary>M6: tab tidur > N detik dibuang (deep-discard). 0 = mati.</summary>
    public int DeepAfterSec { get; set; } = 300;

    /// <summary>M6 (eksperimen): folder ekstensi unpacked → --load-extension Chromium.
    /// TIDAK didukung resmi WebView2 — lihat M6-RESULT untuk hasil uji.</summary>
    public List<string> ExtensionDirs { get; set; } = new();

    /// <summary>M3: adblock bawaan (network + cosmetic). Default NYALA.</summary>
    public bool AdblockEnabled { get; set; } = true;

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
            else if (a.StartsWith("--timezone=", StringComparison.Ordinal))
                Timezone = a["--timezone=".Length..];
            else if (a.StartsWith("--platform=", StringComparison.Ordinal))
                Platform = a["--platform=".Length..];
            else if (a.StartsWith("--proxy=", StringComparison.Ordinal))
                Proxy = a["--proxy=".Length..];
            else if (a.StartsWith("--profile=", StringComparison.Ordinal))
                Profile = a["--profile=".Length..];
            else if (a == "--require-confirm") RequireConfirm = true;
            else if (a.StartsWith("--extension-dir=", StringComparison.Ordinal))
            {
                var d = a["--extension-dir=".Length..].Trim();
                if (d.Length > 0) ExtensionDirs.Add(d);
            }
            else if (a == "--no-restore") RestoreSession = false;
        }
    }

    /// <summary>Simpan config saat ini (dipakai jendela Settings). Tidak pernah melempar.</summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            File.WriteAllText(ConfigPath, System.Text.Json.JsonSerializer.Serialize(this, Indent));
        }
        catch { /* simpan config gagal → biarkan nilai runtime tetap jalan */ }
    }

    /// <summary>
    /// Susun identitas koheren S2 dari config. Null = jangan injeksi apa pun
    /// (tanpa UA kustom, identitas native sudah koheren — mengotak-atik justru menambah jejak).
    /// </summary>
    public Stealth.IdentityProfile? BuildIdentity()
    {
        if (string.IsNullOrWhiteSpace(UserAgent)) return null;
        var id = new Stealth.IdentityProfile
        {
            UserAgent = UserAgent,
            Language = string.IsNullOrWhiteSpace(Language) ? "id-ID" : Language.Trim(),
            Timezone = Timezone?.Trim() ?? "",
            HardwareConcurrency = HardwareConcurrency > 0 ? HardwareConcurrency : 8,
            DeviceMemory = DeviceMemory > 0 ? DeviceMemory : 8,
        };
        id.Languages = id.Language.Equals("en-US", StringComparison.OrdinalIgnoreCase)
            ? new[] { "en-US" } : new[] { id.Language, "en-US" };

        // platform konsisten dengan UA (platform nyata mesin tidak boleh bocor)
        if (!string.IsNullOrWhiteSpace(Platform)) id.Platform = Platform.Trim();
        else if (UserAgent.Contains("Macintosh", StringComparison.Ordinal)) { id.Platform = "MacIntel"; id.UadPlatform = "macOS"; }
        else if (UserAgent.Contains("iPhone", StringComparison.Ordinal) || UserAgent.Contains("iPad", StringComparison.Ordinal)) { id.Platform = "iPhone"; id.UadPlatform = "iOS"; }
        else if (UserAgent.Contains("Android", StringComparison.Ordinal)) { id.Platform = "Linux armv8l"; id.UadPlatform = "Android"; }
        else if (UserAgent.Contains("Linux", StringComparison.Ordinal)) { id.Platform = "Linux x86_64"; id.UadPlatform = "Linux"; }
        // selain itu: Win32 / Windows (default field)

        // userAgentData satu cerita dengan UA: versi brand + platformVersion + mobile
        // (brands asli "Microsoft Edge WebView2" membocorkan engine saat UA menyamar)
        id.UadMobile = UserAgent.Contains("Mobile Safari", StringComparison.Ordinal) ||
                       UserAgent.Contains("iPhone", StringComparison.Ordinal) ||
                       UserAgent.Contains("Android", StringComparison.Ordinal);
        var chromeVer = System.Text.RegularExpressions.Regex.Match(UserAgent, @"Chrome/(\d+)");
        if (chromeVer.Success)
        {
            var v = chromeVer.Groups[1].Value;
            id.UadBrands = new (string, string)[]
            {
                ("Not-A.Brand", "99"), ("Chromium", v), ("Google Chrome", v),
            };
        }
        var pv = System.Text.RegularExpressions.Regex.Match(UserAgent, @"Mac OS X (\d+[_\.]\d+[_\.]?\d*)");
        if (pv.Success) id.UadPlatformVersion = pv.Groups[1].Value.Replace('_', '.');
        else
        {
            var nt = System.Text.RegularExpressions.Regex.Match(UserAgent, @"Windows NT (\d+\.\d+)");
            if (nt.Success) id.UadPlatformVersion = nt.Groups[1].Value + ".0";
            else
            {
                var and = System.Text.RegularExpressions.Regex.Match(UserAgent, @"Android (\d+)");
                if (and.Success) id.UadPlatformVersion = and.Groups[1].Value + ".0.0";
            }
        }

        if (id.Timezone.Length > 0)
        {
            try
            {
                // Windows 11 + ICU: ID IANA ("Asia/Jakarta") didukung langsung
                var tz = TimeZoneInfo.FindSystemTimeZoneById(id.Timezone);
                id.TimezoneOffsetMinutes = -(int)tz.GetUtcOffset(DateTime.UtcNow).TotalMinutes;
            }
            catch
            {
                id.Timezone = ""; // zona tak dikenal → biarkan native (jangan setengah-setengah)
            }
        }
        return id;
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
