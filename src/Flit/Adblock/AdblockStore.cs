using System.Text.Json;

namespace Flit.Adblock;

/// <summary>
/// Penyimpanan & update daftar filter (M3) — mengikuti ADBLOCK-DESIGN.md:
/// cache lokal = fallback, unduh berkala (ETag/If-None-Match) tiap 24 jam,
/// parse di background thread → atomic swap. Gagal unduh ≠ kosongkan blokir.
/// </summary>
public static class AdblockStore
{
    private static volatile FilterEngine? _current;
    /// <summary>Engine aktif — dibaca handler UI-thread per request. Atomic swap saja.</summary>
    public static FilterEngine? Current => _current;

    public static long BlockedCount;                       // dinaikkan handler (interlocked tak wajib utk status)
    public static string Source { get; private set; } = "memuat...";

    /// <summary>Status saklar yang dipilih user (beda dari Current: on tapi masih memuat daftar).</summary>
    public static bool Enabled => _enabled;

    /// <summary>Pecahan JS cosmetic siap-injeksi (DOMContentLoaded + ExecuteScriptAsync).
    /// Diisi tiap swap engine; kosong saat adblock mati.</summary>
    public static volatile string[] CosmeticScriptsCache = Array.Empty<string>();

    private static readonly (string Name, string Url)[] Lists =
    {
        ("easylist", "https://easylist.to/easylist/easylist.txt"),
        ("easyprivacy", "https://easylist.to/easylist/easyprivacy.txt"),
        ("abpindo", "https://raw.githubusercontent.com/ABPindo/indonesianadblockrules/master/subscriptions/abpindo.txt"),
    };

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };
    private static System.Threading.Timer? _refreshTimer;

    public static string FiltersDir => Path.Combine(AgentConfig.ConfigDir, "filters");

    /// <summary>Panggil sekali saat startup (aman dipanggil ulang untuk toggle). Tidak pernah melempar.
    /// onEngineReady dipanggil (thread latar) setiap engine pertama kali siap.</summary>
    public static void Initialize(AgentConfig cfg, Action<FilterEngine>? onEngineReady = null)
    {
        _onEngineReady = onEngineReady;
        if (!_started)
        {
            _enabled = cfg.AdblockEnabled; // config hanya dibaca sekali di startup pertama
            _started = true;
        }
        if (!_enabled)
        {
            Source = "nonaktif";
            return;
        }
        if (_current is not null || _loading) return;

        _loading = true;
        _ = Task.Run(async () =>
        {
            try
            {
                // 1) cache lokal dulu — blokir aktif sejak dini + fallback offline
                var engine = LoadFromCache();
                if (engine is not null)
                {
                    Swap(engine, "cache");
                    AgentLog.Write("adblock.cache", new { engine.NetRules, engine.CosmeticRules });
                }

                // 2) refresh online (ETag) lalu tiap 24 jam
                await RefreshFromNetworkAsync();
                _refreshTimer = new System.Threading.Timer(
                    _ => _ = RefreshFromNetworkAsync(), null,
                    TimeSpan.FromHours(24), TimeSpan.FromHours(24));
            }
            catch (Exception ex)
            {
                AgentLog.Write("adblock.error", ex.Message);
                Source = _current is null ? "gagal memuat" : "cache";
            }
            finally
            {
                _loading = false;
            }
        });
    }

    private static bool _enabled = true;
    private static bool _started;
    private static bool _loading;
    private static Action<FilterEngine>? _onEngineReady;

    /// <summary>Toggle runtime dari menu. Menyalakan = muat cache/unduh lagi.</summary>
    public static void SetEnabled(bool on, AgentConfig cfg, Action<FilterEngine>? onEngineReady = null)
    {
        _enabled = on;
        if (on)
        {
            Initialize(cfg, onEngineReady);
        }
        else
        {
            _current = null;
            CosmeticScriptsCache = Array.Empty<string>();
            Source = "nonaktif";
            AgentLog.Write("adblock", "dimatikan user");
        }
    }

    private static async Task RefreshFromNetworkAsync()
    {
        var changed = false;
        try
        {
            Directory.CreateDirectory(FiltersDir);
            foreach (var (name, url) in Lists)
            {
                var file = Path.Combine(FiltersDir, name + ".txt");
                var etagFile = file + ".etag";
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.UserAgent.ParseAdd("Flit/0.2 (+https://github.com/mrds1111/flit-browser)");
                if (File.Exists(etagFile))
                    req.Headers.IfNoneMatch.TryParseAdd(File.ReadAllText(etagFile));

                using var resp = await Http.SendAsync(req);
                if (resp.StatusCode == System.Net.HttpStatusCode.NotModified) continue;
                if (!resp.IsSuccessStatusCode)
                {
                    AgentLog.Write("adblock.fetch_fail", new { name, status = (int)resp.StatusCode });
                    continue;
                }
                var body = await resp.Content.ReadAsByteArrayAsync();
                if (body.Length < 200) continue; // menjaga dari respons sampah
                await File.WriteAllBytesAsync(file, body);
                var etag = resp.Headers.ETag?.ToString();
                if (etag is not null) await File.WriteAllTextAsync(etagFile, etag);
                changed = true;
                AgentLog.Write("adblock.fetched", new { name, bytes = body.Length });
            }

            // Re-parse penuh hanya saat isi benar-benar berubah (atau belum ada engine)
            if (!changed && _current is not null) return;
            var engine = LoadFromCache();
            if (engine is not null && engine.NetRules > 0)
                Swap(engine, "terbaru");                    // atomic swap
        }
        catch (Exception ex)
        {
            AgentLog.Write("adblock.refresh_error", ex.Message);
        }
    }

    /// <summary>Ganti engine aktif + segarkan cache JS cosmetic + beri tahu UI.</summary>
    private static void Swap(FilterEngine engine, string src)
    {
        _current = engine;
        Source = src;
        try { CosmeticScriptsCache = BuildCosmeticScripts(engine).ToArray(); }
        catch (Exception ex) { CosmeticScriptsCache = Array.Empty<string>(); AgentLog.Write("adblock.cosmetic_build_fail", ex.Message); }
        AgentLog.Write("adblock.swap", new { engine.NetRules, engine.CosmeticRules });
        _onEngineReady?.Invoke(engine);
    }

    private static FilterEngine? LoadFromCache()
    {
        try
        {
            if (!Directory.Exists(FiltersDir)) return null;
            var engine = new FilterEngine();
            var any = false;
            foreach (var (name, _) in Lists)
            {
                var file = Path.Combine(FiltersDir, name + ".txt");
                if (!File.Exists(file)) continue;
                engine.ParseFile(file);
                any = true;
            }
            return any && engine.NetRules > 0 ? engine : null;
        }
        catch { return null; }
    }

    // ---------- injeksi cosmetic ----------

    /// <summary>
    /// JS cosmetic dipecah beberapa script (~350 KB) — script tunggal berukuran >1 MB
    /// pernah gagal dieksekusi senyap oleh WebView2 (terverifikasi uji M3), pecahan kecil aman.
    /// Tiap pecahan self-contained; selektor untuk key yang sama boleh tersebar antar pecahan.
    /// </summary>
    public static List<string> BuildCosmeticScripts(FilterEngine engine)
    {
        var scripts = new List<string>();
        var chunk = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        int size = 0;

        void Flush()
        {
            if (chunk.Count == 0) return;
            scripts.Add(CosmeticJs(JsonSerializer.Serialize(chunk)));
            chunk = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            size = 0;
        }

        void Add(string key, List<string> selectors)
        {
            var json = JsonSerializer.Serialize(selectors);
            if (size + json.Length > 300_000 && chunk.Count > 0) Flush();
            if (!chunk.TryGetValue(key, out var list)) chunk[key] = list = new List<string>();
            list.AddRange(selectors);
            size += json.Length;
            if (size > 400_000) Flush(); // satu domain raksasa pun terpecah
        }

        var global = new List<string>();
        foreach (var s in engine.CosmeticSelectorsGlobal())
            if (IsValidCssSelector(s)) global.Add(s);
        if (global.Count > 0)
        {
            for (int i = 0; i < global.Count; i += 2000)
                Add("*", global.Skip(i).Take(2000).ToList());
        }

        foreach (var (domain, list) in engine.CosmeticByDomain())
        {
            var ok = new List<string>();
            foreach (var s in list)
                if (IsValidCssSelector(s)) ok.Add(s);
            if (ok.Count == 0) continue;
            for (int i = 0; i < ok.Count; i += 2000)
                Add(domain, ok.Skip(i).Take(2000).ToList());
        }
        Flush();
        return scripts;
    }

    private static string CosmeticJs(string json) => """
(function(){
  try{
    var map = %S%;
    var host = location.hostname;
    if(!host) return;
    var out = (map["*"] || []).slice();
    var parts = host.split(".");
    for(var i=0;i<parts.length;i++){
      var k = parts.slice(i).join(".");
      var l = map[k];
      if(l) out = out.concat(l);
    }
    if(!out.length) return;
    function apply(sel){
      var st=document.createElement("style");
      st.setAttribute("data-flit","1");
      st.textContent=sel;
      (document.head||document.documentElement).appendChild(st);
    }
    for(var j=0;j<out.length;j+=100){
      try{ apply(out.slice(j,j+100).join(",")); }catch(e){}
    }
  }catch(e){}
})()
""".Replace("%S%", json);

    /// <summary>Filter kasar: buang selektor bertanda procedural/scriptlet uBO yang bukan CSS valid.</summary>
    private static bool IsValidCssSelector(string s)
    {
        if (s.Length == 0 || s.Length > 500) return false;
        foreach (var ch in s)
        {
            if (char.IsLetterOrDigit(ch)) continue;
            if (" .#[](){}:;,\"'=<>~+*>|^$-_/".IndexOf(ch) >= 0) continue;
            return false;
        }
        return true;
    }
}
